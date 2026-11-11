using EventManagement.Data;
using EventManagement.Services.Ai;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace EventManagement.Controllers;

/// <summary>Endpoints for the AI features: the chat assistant (#11) and recommendations (#12).</summary>
public class AiController : Controller
{
    /// <summary>Chat messages one person (or one IP address, when signed out) may send per hour.</summary>
    public const int MessagesPerHour = 15;

    private readonly AssistantService _assistant;
    private readonly RecommendationService _recommendations;
    private readonly AiUsageLimiter _limiter;
    private readonly UserManager<ApplicationUser> _userManager;

    public AiController(
        AssistantService assistant,
        RecommendationService recommendations,
        AiUsageLimiter limiter,
        UserManager<ApplicationUser> userManager)
    {
        _assistant = assistant;
        _recommendations = recommendations;
        _limiter = limiter;
        _userManager = userManager;
    }

    public sealed class AskRequest
    {
        public string? Message { get; set; }

        /// <summary>Earlier turns, oldest first. The page keeps the conversation; the server is stateless.</summary>
        public List<HistoryTurn>? History { get; set; }
    }

    public sealed class HistoryTurn
    {
        /// <summary>"user" or "assistant".</summary>
        public string? Role { get; set; }

        public string? Text { get; set; }
    }

    /// <summary>POST /assistant/ask with JSON; the anti-forgery token goes in the RequestVerificationToken header.</summary>
    [HttpPost("/assistant/ask")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Ask([FromBody] AskRequest request)
    {
        if (!_assistant.IsAvailable)
        {
            return Json(Reply("The assistant isn't set up on this copy of the site."));
        }

        var message = request.Message?.Trim();
        if (string.IsNullOrEmpty(message))
        {
            return Json(Reply("Ask me anything about events, tickets, the shop or rewards."));
        }

        if (message.Length > AssistantService.MaxQuestionLength)
        {
            return Json(Reply($"Please keep questions under {AssistantService.MaxQuestionLength} characters."));
        }

        var userId = _userManager.GetUserId(User);
        var who = userId ?? $"ip:{HttpContext.Connection.RemoteIpAddress}";
        if (!_limiter.TryConsume($"chat:{who}", MessagesPerHour, TimeSpan.FromHours(1)))
        {
            return Json(Reply($"You've reached the limit of {MessagesPerHour} questions an hour. Please try again a bit later."));
        }

        var history = (request.History ?? [])
            .Where(turn => !string.IsNullOrWhiteSpace(turn.Text))
            .TakeLast(AssistantService.MaxHistoryTurns)
            .Select(turn => new GeminiTurn(
                FromModel: turn.Role == "assistant",
                Text: turn.Text!.Length > 1500 ? turn.Text[..1500] : turn.Text))
            .ToList();

        var reply = await _assistant.AskAsync(userId, message, history, HttpContext.RequestAborted);

        return Json(new
        {
            answer = reply.Answer,
            links = reply.Links.Select(link => new
            {
                title = link.Title,
                subtitle = link.Subtitle,
                kind = link.Kind.ToString().ToLowerInvariant(),
                url = link.Kind switch
                {
                    AssistantLinkKind.Event => Url.Action("Details", "Events", new { area = "", id = link.Id }),
                    AssistantLinkKind.Product => Url.Action("Product", "Shop", new { area = "", id = link.Id }),
                    _ => Url.Action("Vendor", "Shop", new { area = "", id = link.Id })
                }
            })
        });
    }

    /// <summary>GET /recommendations: the "Recommended for you" section, loaded by the page after it renders.</summary>
    [Authorize]
    [HttpGet("/recommendations")]
    public async Task<IActionResult> Recommendations()
    {
        var model = await _recommendations.GetForUserAsync(_userManager.GetUserId(User)!, HttpContext.RequestAborted);
        return PartialView("_Recommendations", model);
    }

    private static object Reply(string answer) => new { answer, links = Array.Empty<object>() };
}
