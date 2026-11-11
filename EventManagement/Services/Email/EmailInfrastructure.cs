using System.Net.Http.Json;
using System.Threading.Channels;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.Extensions.Options;

namespace EventManagement.Services.Email;

/// <summary>One email, ready to send. The body is HTML.</summary>
public sealed record EmailMessage(string ToEmail, string? ToName, string Subject, string HtmlBody);

/// <summary>The "Brevo" configuration section. Without an ApiKey, emails are logged instead of sent.</summary>
public class BrevoOptions
{
    public string? ApiKey { get; set; }

    public string? SenderEmail { get; set; }

    public string SenderName { get; set; } = "Nostalgia Events";

    public bool IsConfigured => !string.IsNullOrWhiteSpace(ApiKey) && !string.IsNullOrWhiteSpace(SenderEmail);
}

/// <summary>The "Email" configuration section.</summary>
public class EmailOptions
{
    /// <summary>
    /// Recipient domains that are never actually emailed, only logged. The
    /// test and demo accounts use test.com, a real domain, so sending to them
    /// would bounce and hurt the sender's reputation with Brevo.
    /// </summary>
    public string[] SuppressedDomains { get; set; } = [];
}

/// <summary>
/// Emails waiting to be sent. Pages add to it and return straight away; the
/// EmailDispatcher sends in the background. It lives in memory, so emails
/// still queued when the app stops are lost, which is fine for notifications.
/// </summary>
public class EmailQueue
{
    private readonly Channel<EmailMessage> _channel = Channel.CreateUnbounded<EmailMessage>();

    public void Enqueue(EmailMessage message) => _channel.Writer.TryWrite(message);

    public IAsyncEnumerable<EmailMessage> ReadAllAsync(CancellationToken cancellationToken) =>
        _channel.Reader.ReadAllAsync(cancellationToken);
}

/// <summary>Sends queued emails one at a time, through Brevo when configured, otherwise to the log.</summary>
public class EmailDispatcher : BackgroundService
{
    private readonly EmailQueue _queue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptionsMonitor<BrevoOptions> _brevoOptions;
    private readonly IOptionsMonitor<EmailOptions> _emailOptions;
    private readonly ILogger<EmailDispatcher> _logger;

    public EmailDispatcher(
        EmailQueue queue,
        IServiceScopeFactory scopeFactory,
        IOptionsMonitor<BrevoOptions> brevoOptions,
        IOptionsMonitor<EmailOptions> emailOptions,
        ILogger<EmailDispatcher> logger)
    {
        _queue = queue;
        _scopeFactory = scopeFactory;
        _brevoOptions = brevoOptions;
        _emailOptions = emailOptions;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_brevoOptions.CurrentValue.IsConfigured)
        {
            _logger.LogInformation("Brevo isn't configured (Brevo:ApiKey and Brevo:SenderEmail), so emails will be written to the log instead of sent.");
        }

        await foreach (var message in _queue.ReadAllAsync(stoppingToken))
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                IEmailDelivery delivery = _brevoOptions.CurrentValue.IsConfigured && !IsSuppressed(message.ToEmail)
                    ? scope.ServiceProvider.GetRequiredService<BrevoEmailDelivery>()
                    : scope.ServiceProvider.GetRequiredService<LogEmailDelivery>();

                await delivery.SendAsync(message, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                // One bad email shouldn't stop the rest.
                _logger.LogError(exception, "Couldn't send email \"{Subject}\" to {Recipient}.", message.Subject, message.ToEmail);
            }
        }
    }

    private bool IsSuppressed(string email)
    {
        var at = email.LastIndexOf('@');
        var domain = at < 0 ? string.Empty : email[(at + 1)..];
        return _emailOptions.CurrentValue.SuppressedDomains
            .Any(suppressed => domain.Equals(suppressed, StringComparison.OrdinalIgnoreCase));
    }
}

public interface IEmailDelivery
{
    Task SendAsync(EmailMessage message, CancellationToken cancellationToken);
}

/// <summary>Sends through Brevo's transactional email API (HTTPS, so it works where SMTP ports are blocked).</summary>
public class BrevoEmailDelivery : IEmailDelivery
{
    private readonly HttpClient _httpClient;
    private readonly BrevoOptions _options;
    private readonly ILogger<BrevoEmailDelivery> _logger;

    public BrevoEmailDelivery(HttpClient httpClient, IOptionsSnapshot<BrevoOptions> options, ILogger<BrevoEmailDelivery> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "v3/smtp/email")
        {
            Content = JsonContent.Create(new
            {
                sender = new { name = _options.SenderName, email = _options.SenderEmail },
                to = new[] { new { email = message.ToEmail, name = message.ToName ?? message.ToEmail } },
                subject = message.Subject,
                htmlContent = message.HtmlBody
            })
        };
        request.Headers.Add("api-key", _options.ApiKey);
        request.Headers.Add("accept", "application/json");

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError("Brevo refused email \"{Subject}\" to {Recipient}: {Status} {Body}",
                message.Subject, message.ToEmail, (int)response.StatusCode, body);
            return;
        }

        _logger.LogInformation("Sent email \"{Subject}\" to {Recipient}.", message.Subject, message.ToEmail);
    }
}

/// <summary>Writes emails to the log, for running without Brevo and for suppressed test addresses.</summary>
public class LogEmailDelivery : IEmailDelivery
{
    private readonly ILogger<LogEmailDelivery> _logger;

    public LogEmailDelivery(ILogger<LogEmailDelivery> logger) => _logger = logger;

    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Email not sent (logged instead) to {Recipient}: \"{Subject}\"\n{Body}",
            message.ToEmail, message.Subject, message.HtmlBody);
        return Task.CompletedTask;
    }
}

/// <summary>
/// The sender ASP.NET Identity uses for account confirmation and password
/// reset emails. Replaces Identity's do-nothing default, so those emails
/// now go through the same queue.
/// </summary>
public class QueuedEmailSender : IEmailSender
{
    private readonly EmailQueue _queue;

    public QueuedEmailSender(EmailQueue queue) => _queue = queue;

    public Task SendEmailAsync(string email, string subject, string htmlMessage)
    {
        _queue.Enqueue(new EmailMessage(email, null, subject, EmailTemplates.Layout(subject, htmlMessage)));
        return Task.CompletedTask;
    }
}
