using EventManagement.Data;
using EventManagement.ViewModels.Admin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EventManagement.Controllers;

[Authorize(Roles = "Admin")]
public class AdminController : Controller
{
    private readonly UserManager<ApplicationUser> _userManager;

    public AdminController(UserManager<ApplicationUser> userManager)
    {
        _userManager = userManager;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var organizerIds = (await _userManager.GetUsersInRoleAsync("Organizer"))
            .Select(user => user.Id)
            .ToHashSet();

        var users = await _userManager.Users
            .OrderBy(user => user.UserName)
            .ToListAsync();

        var model = new AdminIndexViewModel
        {
            Users = users
                .Select(user => new AdminUserRowViewModel
                {
                    Id = user.Id,
                    DisplayName = user.DisplayName,
                    UserName = user.UserName,
                    Email = user.Email,
                    IsOrganizer = organizerIds.Contains(user.Id)
                })
                .ToList()
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetOrganizerRole(string userId, bool makeOrganizer)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return BadRequest();
        }

        var user = await _userManager.FindByIdAsync(userId);
        if (user is null)
        {
            return NotFound();
        }

        var isOrganizer = await _userManager.IsInRoleAsync(user, "Organizer");
        if (isOrganizer == makeOrganizer)
        {
            TempData["StatusMessage"] = "No role change was needed.";
            return RedirectToAction(nameof(Index));
        }

        var result = makeOrganizer
            ? await _userManager.AddToRoleAsync(user, "Organizer")
            : await _userManager.RemoveFromRoleAsync(user, "Organizer");

        TempData["StatusMessage"] = result.Succeeded
            ? makeOrganizer
                ? $"{user.UserName} is now an Organizer. They may need to sign in again for the role to appear in their current session."
                : $"The Organizer role was removed from {user.UserName}. They may need to sign in again for the change to appear in their current session."
            : string.Join(" ", result.Errors.Select(error => error.Description));

        return RedirectToAction(nameof(Index));
    }

}