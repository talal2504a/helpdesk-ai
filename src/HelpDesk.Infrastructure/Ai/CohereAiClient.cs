using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using HelpDesk.Application.Interfaces;
using HelpDesk.Application.DTOs.Messages;
using HelpDesk.Domain.Entities;
using HelpDesk.Infrastructure.Ai;

namespace HelpDesk.Infrastructure.Ai;

public class CohereAiClient : IAiClient
{
    private readonly IHttpClientFactory _factory;
    private readonly ILogger<CohereAiClient> _logger;
    private readonly string? _apiKey;
    private readonly string _model;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_apiKey);

    public CohereAiClient(IHttpClientFactory factory, IConfiguration configuration, ILogger<CohereAiClient> logger)
    {
        _factory = factory;
        _logger = logger;
        _apiKey = configuration.GetSection("Cohere")["ApiKey"] ?? configuration["Cohere__ApiKey"]
                  ?? configuration.GetSection("Ai")["ApiKey"] ?? configuration["Ai__ApiKey"];
        _model = configuration.GetSection("Cohere")["Model"] ?? configuration["Cohere__Model"]
                 ?? configuration.GetSection("Ai")["Model"] ?? configuration["Ai__Model"] ?? "command-r-plus";
    }

    public async Task<AIAnalysis> AnalyzeAsync(string title, string description, CancellationToken ct = default)
    {
        if (!IsConfigured)
            return new RuleBasedAiFallback().Build(title, description);

        var system = """
            You are a help-desk triage engine. Respond ONLY with minified JSON:
            {"category":"one of: Bug Report|Feature Request|Billing Issue|Account Access|How To Question|Other",
             "priority":"Low|Medium|High|Critical","summary":"<=60 word summary","sentiment":"Positive|Neutral|Negative|Angry",
             "suggestedReply":"professional first-response draft for the agent","confidenceScore":0.0-1.0}
            """;

        var payload = new
        {
            model = _model,
            message = $"Ticket title: {title}\n\nTicket description:\n{description}",
            preamble = system,
            temperature = 0.2,
            response_format = new { type = "json_object" }
        };

        try
        {
            var client = _factory.CreateClient("Ai");
            client.BaseAddress = new Uri("https://api.cohere.ai/v2");
            client.DefaultRequestHeaders.Authorization = new("Bearer", _apiKey);

            var resp = await client.PostAsJsonAsync("chat", payload, ct);
            resp.EnsureSuccessStatusCode();

            using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
            var text = doc.RootElement.GetProperty("message").GetProperty("content")[0].GetProperty("text").GetString() ?? "{}";

            using var j = JsonDocument.Parse(text);
            string Get(string p) => j.RootElement.TryGetProperty(p, out var v) ? v.GetString() : null;

            double confidence = 0.75;
            if (j.RootElement.TryGetProperty("confidenceScore", out var cs) && cs.ValueKind == JsonValueKind.Number)
                confidence = cs.GetDouble();
            else if (j.RootElement.TryGetProperty("confidenceScore", out var cs2) && cs2.ValueKind == JsonValueKind.String
                     && double.TryParse(cs2.GetString(), out var d)) confidence = d;

            return new AIAnalysis
            {
                Category = Clamp(Get("category"), RuleBasedAiFallback.Categories),
                Priority = Clamp(Get("priority"), RuleBasedAiFallback.Priorities),
                Summary = Truncate(Get("summary"), 2000),
                Sentiment = Clamp(Get("sentiment"), RuleBasedAiFallback.Sentiments),
                SuggestedReply = Get("suggestedReply"),
                ConfidenceScore = Math.Clamp(confidence, 0, 1),
                Provider = $"Cohere:{_model}"
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Cohere API call failed; falling back to rule-based classification.");
            return new RuleBasedAiFallback().Build(title, description);
        }
    }

    public async Task<string> GenerateReplyAsync(Ticket ticket, int customerId, string customerMessage, IReadOnlyList<MessageDto> conversationHistory, CancellationToken ct = default)
    {
        if (!IsConfigured)
            return await new RuleBasedAiFallback().GenerateReplyAsync(ticket, customerId, customerMessage, conversationHistory, ct);

        if (ticket.Status.Name is "Resolved" or "Closed" || !ticket.AiModeEnabled || ticket.WaitingForAgent)
            return string.Empty;

        var system = """
            You are a professional help-desk agent. Write a concise, empathetic reply to the customer based on the conversation history.
            Be specific to their issue. Do not promise things you cannot verify. Keep it under 120 words unless the issue needs more detail.
            Respond ONLY with the reply text, no JSON, no quotes, no extra formatting.
            """;

        var chatHistory = conversationHistory
            .Select(m => new { role = m.SenderRole == "Customer" ? "user" : "assistant", message = m.Body })
            .ToList();

        var lastMessage = string.IsNullOrWhiteSpace(customerMessage)
            ? $"Ticket: {ticket.Title}\n\nPlease provide a helpful response."
            : $"Ticket: {ticket.Title}\n\nCustomer message: {customerMessage}";

        var payload = new
        {
            model = _model,
            message = lastMessage,
            chat_history = chatHistory,
            preamble = system,
            temperature = 0.4
        };

        try
        {
            var client = _factory.CreateClient("Ai");
            client.BaseAddress = new Uri("https://api.cohere.ai/v2");
            client.DefaultRequestHeaders.Authorization = new("Bearer", _apiKey);

            var resp = await client.PostAsJsonAsync("chat", payload, ct);
            resp.EnsureSuccessStatusCode();

            using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
            var content = doc.RootElement.GetProperty("message").GetProperty("content")[0].GetProperty("text").GetString() ?? "";
            return content.Trim();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Cohere reply generation failed; falling back to rule-based reply.");
            return await new RuleBasedAiFallback().GenerateReplyAsync(ticket, customerId, customerMessage, conversationHistory, ct);
        }
    }

    public async Task<string> GenerateAutoReplyAsync(string ticketTitle, string customerMessage, CancellationToken ct = default)
    {
        if (!IsConfigured)
            return await new RuleBasedAiFallback().GenerateAutoReplyAsync(ticketTitle, customerMessage, ct);

        var system = """
            You are a helpful support assistant. A customer has sent a message and an agent will respond shortly.
            Write a brief, empathetic acknowledgement that shows you understand their issue.
            Address their specific concern in 1-2 sentences, reassure them help is coming, and thank them for their patience.
            Do NOT say you are an AI. Respond ONLY with the message text.
            """;

        var payload = new
        {
            model = _model,
            message = $"Ticket: {ticketTitle}\nCustomer message: {customerMessage}",
            preamble = system,
            temperature = 0.4
        };

        try
        {
            var client = _factory.CreateClient("Ai");
            client.BaseAddress = new Uri("https://api.cohere.ai/v2");
            client.DefaultRequestHeaders.Authorization = new("Bearer", _apiKey);

            var resp = await client.PostAsJsonAsync("chat", payload, ct);
            resp.EnsureSuccessStatusCode();

            using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
            var content = doc.RootElement.GetProperty("message").GetProperty("content")[0].GetProperty("text").GetString() ?? "";
            return content.Trim();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Cohere auto-reply generation failed; falling back to rule-based auto-reply.");
            return await new RuleBasedAiFallback().GenerateAutoReplyAsync(ticketTitle, customerMessage, ct);
        }
    }

    public async Task<AiEscalationResult> ShouldEscalateAsync(string customerMessage, IReadOnlyList<MessageDto> conversationHistory, Ticket ticket, CancellationToken ct = default)
    {
        return await new RuleBasedAiFallback().ShouldEscalateAsync(customerMessage, conversationHistory, ticket, ct);
    }

    private static string? Clamp(string? value, string[] allowed) =>
        value is null ? null : allowed.FirstOrDefault(a => a.Equals(value.Trim(), StringComparison.OrdinalIgnoreCase))
            ?? allowed[0];

    private static string? Truncate(string? s, int max) => s is null ? null : (s.Length <= max ? s : s[..max]);
}
