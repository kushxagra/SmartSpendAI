// SmartSpendAI/Services/AIService.cs

using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using SmartSpendAI.DTOs;

namespace SmartSpendAI.Services;

/// <summary>
/// Calls an OpenAI-compatible chat completions endpoint and asks it to return
/// the invoice as strict JSON. Provider-agnostic: Groq, OpenAI, OpenRouter and
/// Ollama all expose the same /chat/completions contract, so switching provider
/// is a configuration change, not a code change.
/// </summary>
public class AIService
{
    private readonly HttpClient _http;
    private readonly IConfiguration _config;
    private readonly ILogger<AIService> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString
    };

    private const string SystemPrompt = """
        You extract structured data from the text of a supplier invoice.

        Return ONLY a JSON object with exactly these fields:
        {
          "vendorName": string,
          "invoiceNumber": string,
          "invoiceDate": string in YYYY-MM-DD format,
          "dueDate": string in YYYY-MM-DD format,
          "subtotal": number,
          "tax": number,
          "total": number,
          "items": [
            { "description": string, "quantity": number, "unitPrice": number, "total": number }
          ]
        }

        Rules:
        - Use null for any field that is not present in the document.
        - Report exactly what the document states. Do NOT calculate, correct or
          reconcile any figure, even if the numbers do not add up.
        - Numbers must be plain: no currency symbols, no thousands separators, no units.
        - "vendorName" is the company issuing the invoice, not the company being billed.
        - Return the JSON object only. No explanation, no markdown code fences.
        """;

    public AIService(HttpClient http, IConfiguration config, ILogger<AIService> logger)
    {
        _http = http;
        _config = config;
        _logger = logger;
    }

    public async Task<(bool Ok, string? Error, string RawResponse, ExtractedInvoiceDto? Data, string? Model)>
        ExtractAsync(string invoiceText)
    {
        var apiKey = _config["AI:ApiKey"];
        var baseUrl = _config["AI:BaseUrl"]?.TrimEnd('/');
        var model = _config["AI:Model"];

        if (string.IsNullOrWhiteSpace(apiKey))
            return (false, "AI:ApiKey is not configured.", string.Empty, null, model);
        if (string.IsNullOrWhiteSpace(baseUrl) || string.IsNullOrWhiteSpace(model))
            return (false, "AI:BaseUrl or AI:Model is not configured.", string.Empty, null, model);

        var payload = new
        {
            model,
            temperature = 0,
            response_format = new { type = "json_object" },
            messages = new object[]
            {
                new { role = "system", content = SystemPrompt },
                new { role = "user",   content = invoiceText }
            }
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/chat/completions");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        request.Content = new StringContent(
            JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

        HttpResponseMessage response;
        try
        {
            response = await _http.SendAsync(request);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not reach the AI provider.");
            return (false, $"Could not reach the AI provider: {ex.Message}", string.Empty, null, model);
        }

        var body = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("AI provider returned {Status}: {Body}", response.StatusCode, body);
            return (false, $"AI provider returned {(int)response.StatusCode}: {body}", body, null, model);
        }

        // Pull the assistant message out of the chat completion envelope
        string content;
        try
        {
            using var doc = JsonDocument.Parse(body);
            content = doc.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString() ?? string.Empty;
        }
        catch (Exception ex)
        {
            return (false, $"Unexpected response shape from the AI provider: {ex.Message}", body, null, model);
        }

        content = StripCodeFences(content);

        ExtractedInvoiceDto? data;
        try
        {
            data = JsonSerializer.Deserialize<ExtractedInvoiceDto>(content, JsonOptions);
        }
        catch (JsonException ex)
        {
            // Raw content is still returned so it can be stored for auditing
            return (false, $"The AI did not return valid JSON: {ex.Message}", content, null, model);
        }

        if (data is null)
            return (false, "The AI returned an empty result.", content, null, model);

        return (true, null, content, data, model);
    }

    /// <summary>Some models wrap JSON in ```json fences despite being told not to.</summary>
    private static string StripCodeFences(string text)
    {
        var trimmed = text.Trim();
        if (!trimmed.StartsWith("```")) return trimmed;

        var firstNewline = trimmed.IndexOf('\n');
        if (firstNewline < 0) return trimmed;

        trimmed = trimmed[(firstNewline + 1)..];
        var lastFence = trimmed.LastIndexOf("```", StringComparison.Ordinal);
        if (lastFence >= 0) trimmed = trimmed[..lastFence];

        return trimmed.Trim();
    }
}