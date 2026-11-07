using EventManagement.Data;
using EventManagement.Models.Profile;
using EventManagement.ViewModels.Profile;
using EventManagement.Models.Events;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EventManagement.Controllers;

[Authorize]
public class ProfileController : Controller
{
    private readonly ApplicationDbContext _dbContext;
    private readonly UserManager<ApplicationUser> _userManager;

    public ProfileController(
        ApplicationDbContext dbContext,
        UserManager<ApplicationUser> userManager)
    {
        _dbContext = dbContext;
        _userManager = userManager;
    }


    [HttpGet]
    public async Task<IActionResult> Calendar(int? year, int? month)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null)
        {
            return Challenge();
        }

        var utcNow = DateTimeOffset.UtcNow;
        var selectedYear = year ?? utcNow.Year;
        var selectedMonth = month ?? utcNow.Month;

        if (selectedYear < 1900 || selectedYear > 9998 ||
            selectedMonth < 1 || selectedMonth > 12)
        {
            return BadRequest();
        }

        var monthStart = new DateTimeOffset(
            selectedYear, selectedMonth, 1, 0, 0, 0, TimeSpan.Zero);
        var monthEnd = monthStart.AddMonths(1);

        var events = await _dbContext.SavedEvents
            .AsNoTracking()
            .Where(savedEvent =>
                savedEvent.UserId == user.Id &&
                savedEvent.Event.Status == EventStatus.Published &&
                savedEvent.Event.StartsAt < monthEnd &&
                savedEvent.Event.EndsAt >= monthStart)
            .OrderBy(savedEvent => savedEvent.Event.StartsAt)
            .Select(savedEvent => new CalendarEventViewModel
            {
                EventId = savedEvent.EventId,
                Title = savedEvent.Event.Title,
                StartsAt = savedEvent.Event.StartsAt,
                EndsAt = savedEvent.Event.EndsAt
            })
            .ToListAsync();

        var firstOfMonth = new DateOnly(selectedYear, selectedMonth, 1);
        var mondayOffset = ((int)firstOfMonth.DayOfWeek + 6) % 7;
        var gridStart = firstOfMonth.AddDays(-mondayOffset);

        var model = new PersonalCalendarViewModel
        {
            MonthStart = firstOfMonth,
            PreviousMonth = firstOfMonth.AddMonths(-1),
            NextMonth = firstOfMonth.AddMonths(1)
        };

        for (var weekIndex = 0; weekIndex < 6; weekIndex++)
        {
            var week = new List<CalendarDayViewModel>();

            for (var dayIndex = 0; dayIndex < 7; dayIndex++)
            {
                var date = gridStart.AddDays((weekIndex * 7) + dayIndex);

                var dayEvents = events
                    .Where(calendarEvent =>
                    {
                        var startDate = DateOnly.FromDateTime(
                            calendarEvent.StartsAt.UtcDateTime);
                        var endDate = DateOnly.FromDateTime(
                            calendarEvent.EndsAt.UtcDateTime);

                        return startDate <= date && endDate >= date;
                    })
                    .ToList();

                week.Add(new CalendarDayViewModel
                {
                    Date = date,
                    IsInDisplayedMonth = date.Month == selectedMonth,
                    Events = dayEvents
                });
            }

            model.Weeks.Add(week);
        }

        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Edit()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null)
        {
            return Challenge();
        }

        var model = new ProfileEditViewModel
        {
            DisplayName = user.DisplayName ?? user.UserName ?? string.Empty,
            City = user.City,
            PostalCode = user.PostalCode
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(ProfileEditViewModel model)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null)
        {
            return Challenge();
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        user.DisplayName = model.DisplayName.Trim();
        user.City = string.IsNullOrWhiteSpace(model.City)
            ? null
            : model.City.Trim();
        user.PostalCode = string.IsNullOrWhiteSpace(model.PostalCode)
            ? null
            : model.PostalCode.Trim();

        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            return View(model);
        }

        TempData["StatusMessage"] = "Your profile has been updated.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null)
        {
            return Challenge();
        }

        var now = DateTimeOffset.UtcNow;

        var interests = await _dbContext.UserInterests
            .AsNoTracking()
            .Where(userInterest => userInterest.UserId == user.Id)
            .OrderBy(userInterest => userInterest.InterestCategory.Name)
            .Select(userInterest => userInterest.InterestCategory.Name)
            .ToListAsync();

        var savedEvents = await _dbContext.SavedEvents
            .AsNoTracking()
            .Where(savedEvent =>
                savedEvent.UserId == user.Id &&
                savedEvent.Event.Status == EventStatus.Published &&
                savedEvent.Event.EndsAt >= now)
            .OrderBy(savedEvent => savedEvent.Event.StartsAt)
            .Select(savedEvent => new SavedEventSummaryViewModel
            {
                EventId = savedEvent.EventId,
                Title = savedEvent.Event.Title,
                StartsAt = savedEvent.Event.StartsAt,
                VenueName = savedEvent.Event.VenueName,
                City = savedEvent.Event.City
            })
            .ToListAsync();

        var model = new ProfileOverviewViewModel
        {
            DisplayName = string.IsNullOrWhiteSpace(user.DisplayName)
                ? user.UserName ?? "Your profile"
                : user.DisplayName,
            City = user.City,
            PostalCode = user.PostalCode,
            Interests = interests,
            SavedEvents = savedEvents
        };

        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Interests()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null)
        {
            return Challenge();
        }

        var model = new InterestsViewModel
        {
            SelectedInterestIds = await _dbContext.UserInterests
                .Where(userInterest => userInterest.UserId == user.Id)
                .Select(userInterest => userInterest.InterestCategoryId)
                .ToListAsync()
        };

        await LoadAvailableInterestsAsync(model);

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Interests(InterestsViewModel model)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null)
        {
            return Challenge();
        }

        var selectedIds = model.SelectedInterestIds
            .Distinct()
            .ToList();

        var validIds = await _dbContext.InterestCategories
            .Where(category => selectedIds.Contains(category.Id))
            .Select(category => category.Id)
            .ToListAsync();

        if (validIds.Count != selectedIds.Count)
        {
            ModelState.AddModelError(
                nameof(model.SelectedInterestIds),
                "One or more selected interests are invalid.");
        }

        if (!ModelState.IsValid)
        {
            model.SelectedInterestIds = selectedIds;
            await LoadAvailableInterestsAsync(model);
            return View(model);
        }

        var existingInterests = await _dbContext.UserInterests
            .Where(userInterest => userInterest.UserId == user.Id)
            .ToListAsync();

        var idsToRemove = existingInterests
            .Where(userInterest => !selectedIds.Contains(userInterest.InterestCategoryId))
            .ToList();

        _dbContext.UserInterests.RemoveRange(idsToRemove);

        var existingIds = existingInterests
            .Select(userInterest => userInterest.InterestCategoryId)
            .ToHashSet();

        var interestsToAdd = selectedIds
            .Where(id => !existingIds.Contains(id))
            .Select(id => new UserInterest
            {
                UserId = user.Id,
                InterestCategoryId = id
            });

        _dbContext.UserInterests.AddRange(interestsToAdd);

        await _dbContext.SaveChangesAsync();

        TempData["StatusMessage"] = "Your interests have been updated.";
        return RedirectToAction(nameof(Interests));
    }

    private async Task LoadAvailableInterestsAsync(InterestsViewModel model)
    {
        model.AvailableInterests = await _dbContext.InterestCategories
            .AsNoTracking()
            .OrderBy(category => category.Name)
            .Select(category => new InterestOptionViewModel
            {
                Id = category.Id,
                Name = category.Name
            })
            .ToListAsync();
    }
}