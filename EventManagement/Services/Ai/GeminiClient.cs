using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace EventManagement.Services.Ai;

/// <summary>The "Gemini" configuration section (user secrets locally, Gemini__* environment variables on Render).</summary>
public class GeminiOptions
{
    public string? ApiKey { get; set; }

    /// <summary>A model name from Google AI Studio. Defaults to the current Flash model alias.</summary>
    public string? Model { get; set; }

    /// <summary>
    /// A lighter model tried once when the main one is overloaded, slow or out
    /// of free quota (free-tier models are sometimes busy). Defaults to the
    /// current Flash-Lite alias.
    /// </summary>
    public string? FallbackModel { get; set; }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(ApiKey);

    public string ModelOrDefault => string.IsNullOrWhiteSpace(Model) ? "gemini-flash-latest" : Model.Trim();

    public string FallbackModelOrDefault => string.IsNullOrWhiteSpace(FallbackModel) ? "gemini-flash-lite-latest" : FallbackModel.Trim();
}

/// <summary>One turn of a conversation: the user's or the model's.</summary>
public sealed record GeminiTurn(bool FromModel, string Text);

/// <param name="QuotaExceeded">True when Google refused because the free-tier limit was reached.</param>
public sealed record GeminiResult(bool Succeeded, string? Text, bool QuotaExceeded = false);

/// <summary>
/// Calls Google's Gemini API over HTTPS (generateContent). Kept deliberately
/// small: callers pass a system instruction and the conversation, and get
/// back text (or JSON text). If the main model fails or is slow, the fallback
/// model is tried once. Failures are logged and returned, never thrown, so
/// every AI feature can fall back gracefully.
/// </summary>
public class GeminiClient
{
    // Together these keep the worst case under about half a minute.
    private static readonly TimeSpan MainModelTimeout = TimeSpan.FromSeconds(12);
    private static readonly TimeSpan FallbackModelTimeout = TimeSpan.FromSeconds(15);

    private readonly HttpClient _httpClient;
    private readonly GeminiOptions _options;
    private readonly ILogger<GeminiClient> _logger;

    public GeminiClient(HttpClient httpClient, IOptionsSnapshot<GeminiOptions> options, ILogger<GeminiClient> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public bool IsConfigured => _options.IsConfigured;

    /// <param name="jsonResponse">Ask Gemini to reply with JSON only (parse it with <see cref="ParseJson{T}"/>).</param>
    public async Task<GeminiResult> GenerateAsync(
        string systemInstruction,
        IEnumerable<GeminiTurn> conversation,
        bool jsonResponse,
        CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
        {
            return new GeminiResult(false, null);
        }

        var turns = conversation.ToList();
        var result = await CallModelAsync(_options.ModelOrDefault, MainModelTimeout, systemInstruction, turns, jsonResponse, cancellationToken);

        var fallbackModel = _options.FallbackModelOrDefault;
        if (!result.Succeeded && !cancellationToken.IsCancellationRequested && fallbackModel != _options.ModelOrDefault)
        {
            _logger.LogInformation("Trying the fallback model {Model}.", fallbackModel);
            result = await CallModelAsync(fallbackModel, FallbackModelTimeout, systemInstruction, turns, jsonResponse, cancellationToken);
        }

        return result;
    }

    private async Task<GeminiResult> CallModelAsync(
        string model,
        TimeSpan timeLimit,
        string systemInstruction,
        IReadOnlyList<GeminiTurn> conversation,
        bool jsonResponse,
        CancellationToken cancellationToken)
    {
        var body = new
        {
            systemInstruction = new { parts = new[] { new { text = systemInstruction } } },
            contents = conversation.Select(turn => new
            {
                role = turn.FromModel ? "model" : "user",
                parts = new[] { new { text = turn.Text } }
            }),
            generationConfig = new
            {
                temperature = 0.4,
                // Generous, because newer models spend part of this on thinking.
                maxOutputTokens = 4096,
                responseMimeType = jsonResponse ? "application/json" : "text/plain"
            }
        };

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"v1beta/models/{Uri.EscapeDataString(model)}:generateContent")
        {
            Content = JsonContent.Create(body)
        };
        request.Headers.Add("x-goog-api-key", _options.ApiKey);

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(timeLimit);

        try
        {
            using var response = await _httpClient.SendAsync(request, timeout.Token);
            var responseText = await response.Content.ReadAsStringAsync(timeout.Token);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Gemini ({Model}) returned {Status}: {Body}",
                    model, (int)response.StatusCode, responseText.Length > 500 ? responseText[..500] : responseText);
                return new GeminiResult(false, null, QuotaExceeded: response.StatusCode == HttpStatusCode.TooManyRequests);
            }

            var text = ExtractText(responseText);
            if (string.IsNullOrWhiteSpace(text))
            {
                _logger.LogWarning("Gemini returned no text (possibly blocked by safety settings).");
                return new GeminiResult(false, null);
            }

            return new GeminiResult(true, text);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("Gemini ({Model}) didn't answer within {Seconds} seconds.", model, timeLimit.TotalSeconds);
            return new GeminiResult(false, null);
        }
        catch (Exception exception) when (exception is HttpRequestException or JsonException)
        {
            _logger.LogWarning(exception, "Gemini request failed.");
            return new GeminiResult(false, null);
        }
    }

    /// <summary>Reads JSON from a reply, tolerating a ```json fence around it. Returns default if it doesn't parse.</summary>
    public static T? ParseJson<T>(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return default;
        }

        var json = text.Trim();
        if (json.StartsWith("```", StringComparison.Ordinal))
        {
            var firstLineEnd = json.IndexOf('\n');
            var closingFence = json.LastIndexOf("```", StringComparison.Ordinal);
            if (firstLineEnd > 0 && closingFence > firstLineEnd)
            {
                json = json[(firstLineEnd + 1)..closingFence];
            }
        }

        try
        {
            return JsonSerializer.Deserialize<T>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (JsonException)
        {
            return default;
        }
    }

    /// <summary>Joins the text parts of the first candidate.</summary>
    private static string? ExtractText(string responseJson)
    {
        using var document = JsonDocument.Parse(responseJson);
        if (!document.RootElement.TryGetProperty("candidates", out var candidates) ||
            candidates.GetArrayLength() == 0 ||
            !candidates[0].TryGetProperty("content", out var content) ||
            !content.TryGetProperty("parts", out var parts))
        {
            return null;
        }

        var text = new StringBuilder();
        foreach (var part in parts.EnumerateArray())
        {
            // Skip "thought" parts some models include; keep only the answer.
            if (part.TryGetProperty("thought", out var thought) && thought.ValueKind == JsonValueKind.True)
            {
                continue;
            }

            if (part.TryGetProperty("text", out var value))
            {
                text.Append(value.GetString());
            }
        }

        return text.ToString();
    }
}

/// <summary>
/// Caps how often one person can use the AI features, so a single user
/// can't burn through the shared free-tier quota. Counts live in memory.
/// </summary>
public class AiUsageLimiter
{
    private readonly IMemoryCache _cache;
    private readonly object _lock = new();

    public AiUsageLimiter(IMemoryCache cache) => _cache = cache;

    /// <summary>Records one use; false when the caller is already at the limit for this window.</summary>
    public bool TryConsume(string key, int limit, TimeSpan window)
    {
        lock (_lock)
        {
            var cacheKey = $"ai-usage:{key}";
            var count = _cache.GetOrCreate(cacheKey, entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = window;
                return new StrongBox<int>(0);
            })!;

            if (count.Value >= limit)
            {
                return false;
            }

            count.Value++;
            return true;
        }
    }

    private sealed class StrongBox<T>(T value)
    {
        public T Value { get; set; } = value;
    }
}
