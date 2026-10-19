using EventManagement.Data;
using EventManagement.Models;
using EventManagement.Services;
using EventManagement.ViewModels.Events;
using EventManagement.ViewModels.Home;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;

namespace EventManagement.Controllers
{
    public class HomeController : Controller
    {
        private const int PopularEventCount = 6;

        private readonly EventDiscoveryService _eventDiscoveryService;
        private readonly UserManager<ApplicationUser> _userManager;

        public HomeController(
            EventDiscoveryService eventDiscoveryService,
            UserManager<ApplicationUser> userManager)
        {
            _eventDiscoveryService = eventDiscoveryService;
            _userManager = userManager;
        }

        public async Task<IActionResult> Index()
        {
            var userId = _userManager.GetUserId(User);

            var eventList = await _eventDiscoveryService.GetEventsAsync(
                new EventFilterViewModel(),
                userId,
                PopularEventCount);

            var model = new HomeIndexViewModel
            {
                PopularEvents = eventList.Events,
                InterestCategories = eventList.InterestCategories
            };

            return View(model);
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }

        public IActionResult SetSelectedEvent()
        {
            HttpContext.Session.SetString("SelectedEventName", "Spring Fundraiser");

            return RedirectToAction(nameof(SelectedEvent));
        }

        public IActionResult SelectedEvent()
        {
            ViewBag.SelectedEventName =
                HttpContext.Session.GetString("SelectedEventName")
                ?? "No event has been selected.";

            return View();
        }
    }
}
