using EventManagement.Data;
using EventManagement.Models.Applications;
using EventManagement.Models.Commerce;
using EventManagement.Models.Vendors;
using EventManagement.Services.Email;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using static EventManagement.Services.Email.EmailTemplates;

namespace EventManagement.Services;

/// <summary>
/// Builds the notification emails and puts them on the queue. Called by the
/// services after something has actually happened (an order placed, an
/// announcement posted, an application submitted or decided), never from the
/// database, so seeded data never sends anything.
/// <para>
/// Every method catches its own errors: a failed email is logged and never
/// undoes or blocks the action that triggered it.
/// </para>
/// </summary>
public class EmailNotificationService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly EmailQueue _queue;
    private readonly LinkGenerator _links;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<EmailNotificationService> _logger;

    public EmailNotificationService(
        ApplicationDbContext dbContext,
        EmailQueue queue,
        LinkGenerator links,
        IHttpContextAccessor httpContextAccessor,
        ILogger<EmailNotificationService> logger)
    {
        _dbContext = dbContext;
        _queue = queue;
        _links = links;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    // ----- Orders --------------------------------------------------------------

    public Task OrderPlacedAsync(int orderId, CancellationToken cancellationToken = default) =>
        SafelyAsync($"order #{orderId} confirmation", async () =>
        {
            var order = await _dbContext.Orders
                .AsNoTracking()
                .Where(item => item.Id == orderId)
                .Select(item => new
                {
                    item.Id,
                    item.TotalAmount,
                    item.RewardDiscount,
                    item.PointsRedeemed,
                    item.PointsEarned,
                    Email = item.User.Email,
                    Name = item.User.DisplayName,
                    Items = item.Items.Select(line => new { line.ProductName, line.VendorName, line.UnitPrice, line.Quantity }).ToList()
                })
                .SingleOrDefaultAsync(cancellationToken);

            if (order?.Email is null)
            {
                return;
            }

            var tickets = await _dbContext.Tickets
                .AsNoTracking()
                .Where(ticket => ticket.OrderId == orderId)
                .Select(ticket => new
                {
                    EventTitle = ticket.EventTicketType.Event.Title,
                    EventStartsAt = ticket.EventTicketType.Event.StartsAt,
                    TicketTypeName = ticket.EventTicketType.Name,
                    ticket.UnitPrice
                })
                .ToListAsync(cancellationToken);

            var rows = tickets
                .GroupBy(ticket => ticket)
                .Select(group => (
                    $"{Encode(group.Key.EventTitle)} &middot; {Encode(group.Key.TicketTypeName)} &times; {group.Count()}" +
                    $"<br><span style=\"color:#6b7280;font-size:13px;\">{group.Key.EventStartsAt.LocalDateTime:ddd, MMM d, yyyy h:mm tt}</span>",
                    Encode((group.Key.UnitPrice * group.Count()).ToString("C"))))
                .Concat(order.Items.Select(line => (
                    $"{Encode(line.ProductName)} &times; {line.Quantity}<br><span style=\"color:#6b7280;font-size:13px;\">Sold by {Encode(line.VendorName)}</span>",
                    Encode((line.UnitPrice * line.Quantity).ToString("C")))))
                .ToList();

            if (order.RewardDiscount > 0)
            {
                rows.Add(($"Rewards ({order.PointsRedeemed} points)", $"&minus;{Encode(order.RewardDiscount.ToString("C"))}"));
            }

            var body =
                Paragraph($"Thanks{(order.Name is null ? string.Empty : ", " + Encode(order.Name))}! Your order is confirmed.") +
                Table(rows, "Total paid", Encode(order.TotalAmount.ToString("C"))) +
                (order.PointsEarned > 0 ? Paragraph($"You earned <strong>{order.PointsEarned} reward points</strong> with this order.") : string.Empty) +
                (tickets.Count > 0 ? Paragraph("Your ticket codes are on the My tickets page.") : string.Empty) +
                Paragraph("<span style=\"color:#6b7280;font-size:13px;\">This was a demo checkout: no payment was taken and merchandise won't be shipped.</span>");

            Send(order.Email, order.Name, $"Order #{order.Id} confirmed",
                Layout($"Order #{order.Id} confirmed", body, "View your order", ActionUrl("Details", "Orders", new { id = order.Id })));
        });

    // ----- Announcements -------------------------------------------------------

    /// <summary>Emails a new announcement to everyone holding a ticket for the event, except whoever posted it.</summary>
    public Task AnnouncementPostedAsync(int announcementId, CancellationToken cancellationToken = default) =>
        SafelyAsync($"announcement {announcementId}", async () =>
        {
            var announcement = await _dbContext.EventAnnouncements
                .AsNoTracking()
                .Where(item => item.Id == announcementId)
                .Select(item => new { item.EventId, item.Title, item.Body, item.PostedByUserId, EventTitle = item.Event.Title })
                .SingleOrDefaultAsync(cancellationToken);

            if (announcement is null)
            {
                return;
            }

            var recipients = await _dbContext.Tickets
                .AsNoTracking()
                .Where(ticket =>
                    ticket.EventTicketType.EventId == announcement.EventId &&
                    ticket.Order.Status == OrderStatus.Completed &&
                    ticket.Order.UserId != announcement.PostedByUserId)
                .Select(ticket => new { ticket.Order.User.Email, ticket.Order.User.DisplayName })
                .Distinct()
                .ToListAsync(cancellationToken);

            var url = ActionUrl("Details", "Events", new { id = announcement.EventId }, "announcements");
            var html = Layout(
                announcement.Title,
                Paragraph($"The organizer of <strong>{Encode(announcement.EventTitle)}</strong> posted an update:") +
                Quote(EncodeMultiline(announcement.Body)) +
                Paragraph("You're getting this because you have tickets for this event."),
                "See the event",
                url);

            foreach (var recipient in recipients.Where(recipient => recipient.Email is not null))
            {
                Send(recipient.Email!, recipient.DisplayName, $"{announcement.EventTitle}: {announcement.Title}", html);
            }
        });

    // ----- Vendor applications -------------------------------------------------

    /// <summary>Tells the event's organizer a vendor applied to sell at their event.</summary>
    public Task VendorApplicationSubmittedAsync(int applicationId, CancellationToken cancellationToken = default) =>
        SafelyAsync($"vendor application {applicationId} alert", async () =>
        {
            var application = await _dbContext.VendorApplications
                .AsNoTracking()
                .Where(item => item.Id == applicationId && item.Kind == VendorApplicationKind.Event)
                .Select(item => new
                {
                    item.BusinessName,
                    item.ProductsOffered,
                    EventId = item.EventId!.Value,
                    EventTitle = item.Event!.Title,
                    OrganizerEmail = item.Event.Organizer.Email,
                    OrganizerName = item.Event.Organizer.DisplayName
                })
                .SingleOrDefaultAsync(cancellationToken);

            if (application?.OrganizerEmail is null)
            {
                return;
            }

            Send(application.OrganizerEmail, application.OrganizerName, $"New vendor application for {application.EventTitle}",
                Layout("New vendor application",
                    Paragraph($"<strong>{Encode(application.BusinessName)}</strong> wants to sell at <strong>{Encode(application.EventTitle)}</strong>.") +
                    Quote(Encode(application.ProductsOffered)),
                    "Review vendors",
                    PageUrl("/Events/Vendors", "Organizer", new { id = application.EventId })));
        });

    public Task VendorApplicationDecidedAsync(int applicationId, CancellationToken cancellationToken = default) =>
        SafelyAsync($"vendor application {applicationId} decision", async () =>
        {
            var application = await _dbContext.VendorApplications
                .AsNoTracking()
                .Where(item => item.Id == applicationId)
                .Select(item => new
                {
                    item.Kind,
                    item.Status,
                    item.BusinessName,
                    item.ContactEmail,
                    item.ReviewNote,
                    EventTitle = item.Event != null ? item.Event.Title : null
                })
                .SingleOrDefaultAsync(cancellationToken);

            if (application is null)
            {
                return;
            }

            var approved = application.Status == ApplicationStatus.Approved;
            var what = application.Kind == VendorApplicationKind.OnlineSales
                ? "sell merchandise on Nostalgia Events"
                : $"sell at {application.EventTitle}";
            var next = !approved
                ? "You're welcome to apply again."
                : application.Kind == VendorApplicationKind.OnlineSales
                    ? "You can now add products to the shop from My products."
                    : "You'll be listed on the event page as one of its vendors.";

            SendDecision(application.ContactEmail, application.BusinessName, approved,
                $"Your application to {what}",
                $"Your application from <strong>{Encode(application.BusinessName)}</strong> to {Encode(what)} was <strong>{(approved ? "approved" : "not approved")}</strong>.",
                application.ReviewNote, next,
                "View your applications", PageUrl("/Applications/Index", "Vendor", null));
        });

    // ----- Panel proposals -----------------------------------------------------

    /// <summary>Tells the event's organizer someone proposed a panel.</summary>
    public Task PanelApplicationSubmittedAsync(int applicationId, CancellationToken cancellationToken = default) =>
        SafelyAsync($"panel proposal {applicationId} alert", async () =>
        {
            var application = await _dbContext.PanelApplications
                .AsNoTracking()
                .Where(item => item.Id == applicationId)
                .Select(item => new
                {
                    item.Title,
                    item.Description,
                    item.EventId,
                    EventTitle = item.Event.Title,
                    OrganizerEmail = item.Event.Organizer.Email,
                    OrganizerName = item.Event.Organizer.DisplayName
                })
                .SingleOrDefaultAsync(cancellationToken);

            if (application?.OrganizerEmail is null)
            {
                return;
            }

            Send(application.OrganizerEmail, application.OrganizerName, $"New panel proposal for {application.EventTitle}",
                Layout("New panel proposal",
                    Paragraph($"Someone proposed <strong>{Encode(application.Title)}</strong> for <strong>{Encode(application.EventTitle)}</strong>.") +
                    Quote(EncodeMultiline(application.Description)),
                    "Review panels",
                    PageUrl("/Events/Panels", "Organizer", new { id = application.EventId })));
        });

    public Task PanelApplicationDecidedAsync(int applicationId, CancellationToken cancellationToken = default) =>
        SafelyAsync($"panel proposal {applicationId} decision", async () =>
        {
            var application = await _dbContext.PanelApplications
                .AsNoTracking()
                .Where(item => item.Id == applicationId)
                .Select(item => new
                {
                    item.Status,
                    item.Title,
                    item.ContactEmail,
                    item.ReviewNote,
                    item.EventId,
                    EventTitle = item.Event.Title,
                    ApplicantName = item.Applicant.DisplayName
                })
                .SingleOrDefaultAsync(cancellationToken);

            if (application is null)
            {
                return;
            }

            var approved = application.Status == ApplicationStatus.Approved;
            SendDecision(application.ContactEmail, application.ApplicantName, approved,
                $"Your panel proposal for {application.EventTitle}",
                $"Your panel <strong>{Encode(application.Title)}</strong> for <strong>{Encode(application.EventTitle)}</strong> was <strong>{(approved ? "approved" : "not approved")}</strong>.",
                application.ReviewNote,
                approved ? "It's now listed on the event page." : "You're welcome to propose a different panel.",
                approved ? "See the event" : "View your proposals",
                approved ? ActionUrl("Details", "Events", new { id = application.EventId }) : PageUrl("/Index", "Panels", null));
        });

    // ----- Event (hosting) applications ----------------------------------------

    public Task EventApplicationDecidedAsync(int applicationId, CancellationToken cancellationToken = default) =>
        SafelyAsync($"event application {applicationId} decision", async () =>
        {
            var application = await _dbContext.EventApplications
                .AsNoTracking()
                .Where(item => item.Id == applicationId)
                .Select(item => new { item.Status, item.Title, item.ContactEmail, item.ContactName, item.ReviewNote, item.CreatedEventId })
                .SingleOrDefaultAsync(cancellationToken);

            if (application is null)
            {
                return;
            }

            var approved = application.Status == ApplicationStatus.Approved;
            SendDecision(application.ContactEmail, application.ContactName, approved,
                $"Your application to list {application.Title}",
                $"Your application to list <strong>{Encode(application.Title)}</strong> was <strong>{(approved ? "approved" : "not approved")}</strong>.",
                application.ReviewNote,
                approved
                    ? "Your event is now live, and you're its organizer. Sign out and back in once to see the organizer tools."
                    : "You're welcome to apply again with changes.",
                approved ? "See your event" : "View your applications",
                approved && application.CreatedEventId is int eventId
                    ? ActionUrl("Details", "Events", new { id = eventId })
                    : PageUrl("/Index", "Hosting", null));
        });

    // ----- Helpers -------------------------------------------------------------

    private void SendDecision(
        string toEmail, string? toName, bool approved, string subjectAbout, string summaryHtml,
        string? reviewNote, string nextStep, string buttonText, string? buttonUrl)
    {
        var body =
            Paragraph(summaryHtml) +
            (reviewNote is null ? string.Empty : Paragraph("Note from the reviewer:") + Quote(EncodeMultiline(reviewNote))) +
            Paragraph(Encode(nextStep));

        Send(toEmail, toName, $"{(approved ? "Approved" : "Update")}: {subjectAbout}",
            Layout(approved ? "Good news!" : "About your application", body, buttonText, buttonUrl));
    }

    private void Send(string toEmail, string? toName, string subject, string html) =>
        _queue.Enqueue(new EmailMessage(toEmail, toName, subject, html));

    private string? ActionUrl(string action, string controller, object values, string? fragment = null)
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext is null)
        {
            return null;
        }

        var routeValues = new RouteValueDictionary(values) { ["area"] = string.Empty };
        return _links.GetUriByAction(httpContext, action, controller, routeValues,
            fragment: fragment is null ? default : new FragmentString("#" + fragment));
    }

    private string? PageUrl(string page, string area, object? values)
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext is null)
        {
            return null;
        }

        var routeValues = new RouteValueDictionary(values) { ["area"] = area };
        return _links.GetUriByPage(httpContext, page, handler: null, values: routeValues);
    }

    private async Task SafelyAsync(string what, Func<Task> build)
    {
        try
        {
            await build();
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Couldn't prepare the email for {What}.", what);
        }
    }
}
