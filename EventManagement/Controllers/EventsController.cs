using EventManagement.Data;
using EventManagement.Services;
using EventManagement.ViewModels.Events;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace EventManagement.Controllers;

public class EventsController : Controller
{
    private readonly EventDiscoveryService _eventDiscoveryService;
    private readonly UserManager<ApplicationUser> _userManager;

    public EventsController(
        EventDiscoveryService eventDiscoveryService,
        UserManager<ApplicationUser> userManager)
    {
        _eventDiscoveryService = eventDiscoveryService;
        _userManager = userManager;
    }

    [HttpGet]
    public async Task<IActionResult> Index(EventFilterViewModel filter)
    {
        var userId = _userManager.GetUserId(User);

        var model = await _eventDiscoveryService.GetEventsAsync(
            filter,
            userId);

        return View(model);
    }

    public IActionResult Details(int id)
    {
        return View();
    }
}