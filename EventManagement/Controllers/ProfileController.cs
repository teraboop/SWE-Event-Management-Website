using EventManagement.Data;
using EventManagement.Models.Profile;
using EventManagement.ViewModels.Profile;
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