using EventManagement.Data;
using EventManagement.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace EventManagement.Controllers;

/// <summary>My rewards: balance, tier, how to earn, and points history.</summary>
[Authorize]
public class RewardsController : Controller
{
    private readonly RewardService _rewardService;
    private readonly UserManager<ApplicationUser> _userManager;

    public RewardsController(RewardService rewardService, UserManager<ApplicationUser> userManager)
    {
        _rewardService = rewardService;
        _userManager = userManager;
    }

    [HttpGet]
    public async Task<IActionResult> Index() =>
        View(await _rewardService.GetSummaryAsync(_userManager.GetUserId(User)!, HttpContext.RequestAborted));
}
