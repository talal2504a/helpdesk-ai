using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using HelpDesk.Application.Interfaces;
using HelpDesk.Application.DTOs.Messages;
using HelpDesk.Application.DTOs.Ai;
using HelpDesk.Domain.Entities;

namespace HelpDesk.Infrastructure.Ai;

public class OpenAiCompatibleAiClient : IAiClient
{
    private readonly IHttpClientFactory _factory;
    private readonly ILogger<OpenAiCompatibleAiClient> _logger;
    private readonly string? _apiKey;
    private readonly string _baseUrl;
    private readonly string _model;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_apiKey);

    public OpenAiCompatibleAiClient(IHttpClientFactory factory, IConfiguration configuration, ILogger<OpenAiCompatibleAiClient> logger)
    {
        _factory = factory;
        _logger = logger;
        _apiKey = configuration.GetSection("Ai")["ApiKey"] ?? configuration["Ai__ApiKey"];
        _baseUrl = (configuration.GetSection("Ai")["BaseUrl"] ?? configuration["Ai__BaseUrl"] ?? "https://api.openai.com/v1").TrimEnd('/') + "/";
        _model = configuration.GetSection("Ai")["Model"] ?? configuration["Ai__Model"] ?? "gpt-4o-mini";
    }

    private static readonly string[] HumanRequestPatterns = new[]
    {
        "where is the agent", "where is your support agent", "i want a real person",
        "connect me to an agent", "i don't want to talk to ai", "give me a human",
        "i want to speak to a human", "i need a human", "human support", "real agent",
        "talk to a person", "speak to agent", "customer service", "live agent",
        "where is the human", "not ai", "not bot"
    };

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

        var messages = new[]
        {
            new { role = "system", content = system },
            new { role = "user", content = $"Ticket title: {title}\n\nTicket description:\n{description}" }
        };

        try
        {
            var client = _factory.CreateClient("Ai");
            client.BaseAddress = new Uri(_baseUrl);
            client.DefaultRequestHeaders.Authorization = new("Bearer", _apiKey);

            var resp = await client.PostAsJsonAsync("chat/completions", new
            {
                model = _model,
                messages,
                temperature = 0.2,
                response_format = new { type = "json_object" }
            }, ct);
            resp.EnsureSuccessStatusCode();

            using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
            var text = doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? "{}";

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
                Provider = $"OpenAiCompatible:{_model}"
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "AI chat-completions call failed ({BaseUrl}); falling back to rule-based classification.", _baseUrl);
            return new RuleBasedAiFallback().Build(title, description);
        }
    }

    public async Task<string> GenerateReplyAsync(Ticket ticket, int customerId, string customerMessage, IReadOnlyList<MessageDto> conversationHistory, CancellationToken ct = default)
    {
        if (!IsConfigured)
            return await new RuleBasedAiFallback().GenerateReplyAsync(ticket, customerId, customerMessage, conversationHistory, ct);

        if (ticket.Status.Name == "Closed" || !ticket.AiModeEnabled)
            return string.Empty;

        var historyMsgs = new List<object>();
        foreach (var m in conversationHistory.TakeLast(20))
        {
            if (m.SenderRole == "Customer")
                historyMsgs.Add(new { role = "user", content = m.Body });
            else if (m.IsAiGenerated)
                historyMsgs.Add(new { role = "assistant", content = $"[AI Support]: {m.Body}" });
            else if (m.SenderRole == "Agent" || m.SenderRole == "Admin")
                historyMsgs.Add(new { role = "assistant", content = $"[Agent {m.SenderName}]: {m.Body}" });
        }

        var systemPrompt = $"""
            You are a professional help-desk AI support agent for a customer support platform.
            
            RULES:
            1. Understand the customer's problem from the full conversation context.
            2. Ask relevant follow-up questions if you need more information.
            3. Be empathetic and natural, not robotic.
            4. Remember previous messages and reference them naturally.
            5. Never make up information or promise things you cannot verify.
            6. Keep replies concise (under 120 words) unless the issue needs more detail.
            7. If the customer asks for a human agent (keywords: "where is the agent", "real person", "connect me to an agent", "human", "not ai", "talk to a person"), DO NOT continue normal conversation. Instead say something like: "I understand you'd like to speak with a human agent. Let me connect you right away. Please hold for a moment."
            8. If the customer has already been transferred and asks again, say: "Your request has already been transferred to a human support agent. An agent will continue this conversation shortly." and stop engaging further.
            9. Respond ONLY with the reply text, no JSON, no quotes, no extra formatting.
            10. If "Escalated to human agent" below is YES, reassure the customer that an agent has been notified and is on the way, and keep helping them in the meantime. Never leave their messages unanswered.
            
            Current ticket: {ticket.Title}
            Ticket description: {ticket.Description}
            Customer name: {ticket.User.Name}
            Escalated to human agent: {(ticket.WaitingForAgent ? "YES" : "no")}
            """;

        var msgs = new List<object> { new { role = "system", content = systemPrompt } };
        msgs.AddRange(historyMsgs);
        msgs.Add(new { role = "user", content = customerMessage });

        try
        {
            var client = _factory.CreateClient("Ai");
            client.BaseAddress = new Uri(_baseUrl);
            client.DefaultRequestHeaders.Authorization = new("Bearer", _apiKey);

            var resp = await client.PostAsJsonAsync("chat/completions", new { model = _model, messages = msgs, temperature = 0.4 }, ct);
            resp.EnsureSuccessStatusCode();

            using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
            var reply = (doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? "").Trim();

            // Strip a speaker prefix like "[AI Support]:" or "[Agent Name]:" if the model copied it from history.
            if (reply.StartsWith("[", StringComparison.Ordinal))
            {
                var idx = reply.IndexOf("]:", StringComparison.Ordinal);
                if (idx > -1 && idx < 40)
                    reply = reply[(idx + 2)..].Trim();
            }

            return reply;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "AI reply generation failed ({BaseUrl}); falling back to rule-based reply.", _baseUrl);
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

        var messages = new[]
        {
            new { role = "system", content = system },
            new { role = "user", content = $"Ticket: {ticketTitle}\nCustomer message: {customerMessage}" }
        };

        try
        {
            var client = _factory.CreateClient("Ai");
            client.BaseAddress = new Uri(_baseUrl);
            client.DefaultRequestHeaders.Authorization = new("Bearer", _apiKey);

            var resp = await client.PostAsJsonAsync("chat/completions", new { model = _model, messages, temperature = 0.4 }, ct);
            resp.EnsureSuccessStatusCode();

            using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
            return (doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? "").Trim();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "AI auto-reply generation failed ({BaseUrl}); falling back to rule-based auto-reply.", _baseUrl);
            return await new RuleBasedAiFallback().GenerateAutoReplyAsync(ticketTitle, customerMessage, ct);
        }
    }

    public async Task<AiEscalationResult> ShouldEscalateAsync(string customerMessage, IReadOnlyList<MessageDto> conversationHistory, Ticket ticket, CancellationToken ct = default)
    {
        if (!IsConfigured)
            return await new RuleBasedAiFallback().ShouldEscalateAsync(customerMessage, conversationHistory, ticket, ct);

        var lowerMessage = customerMessage.ToLowerInvariant();
        foreach (var pattern in HumanRequestPatterns)
        {
            if (lowerMessage.Contains(pattern))
            {
                var alreadyEscalated = ticket.WaitingForAgent;
                return new AiEscalationResult
                {
                    ShouldEscalate = true,
                    Reason = "Customer requested human agent",
                    SuggestedResponse = alreadyEscalated
                        ? "Your request has already been transferred to a human support agent. An agent will continue this conversation shortly."
                        : "I understand you'd like to speak with a human agent. Let me connect you right away. Please hold for a moment."
                };
            }
        }

        var escalatedCount = conversationHistory.Count(m => m.SenderRole == "AI");
        var customerMsgs = conversationHistory.Count(m => m.SenderRole == "Customer");
        if (escalatedCount > 0 && customerMsgs > 6)
        {
            return new AiEscalationResult
            {
                ShouldEscalate = true,
                Reason = "Extended conversation without resolution",
                SuggestedResponse = "I want to make sure you get the best help possible. Let me connect you with a specialist who can assist you further."
            };
        }

        var system = """
            You are a support triage system. Analyze the customer's message and conversation history.
            Respond ONLY with minified JSON: {"escalate": true/false, "reason": "brief reason", "response": "suggested AI response"}
            
            ESCALATE if:
            - Customer explicitly asks for human agent
            - Issue involves billing refund, account deletion, legal matters
            - Customer is very angry/frustrated after multiple exchanges
            - Issue requires human approval or verification
            - AI cannot resolve the issue adequately
            
            Do NOT escalate for simple questions or routine issues.
            """;

        var historySummary = string.Join("\n", conversationHistory.TakeLast(10).Select(m =>
            $"[{m.SenderRole}]: {m.Body}"));

        var msgs = new object[]
        {
            new { role = "system", content = system },
            new { role = "user", content = $"Ticket: {ticket.Title}\nDescription: {ticket.Description}\n\nConversation:\n{historySummary}\n\nNew message: {customerMessage}" }
        };

        try
        {
            var client = _factory.CreateClient("Ai");
            client.BaseAddress = new Uri(_baseUrl);
            client.DefaultRequestHeaders.Authorization = new("Bearer", _apiKey);

            var resp = await client.PostAsJsonAsync("chat/completions", new { model = _model, messages = msgs, temperature = 0.2, response_format = new { type = "json_object" } }, ct);
            resp.EnsureSuccessStatusCode();

            using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
            var text = doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? "{}";

            using var j = JsonDocument.Parse(text);
            bool escalate = j.RootElement.TryGetProperty("escalate", out var esc) && esc.ValueKind == JsonValueKind.True;
            string reason = j.RootElement.TryGetProperty("reason", out var r) && r.ValueKind == JsonValueKind.String ? r.GetString()! : "AI determined escalation needed";
            string response = j.RootElement.TryGetProperty("response", out var res) && res.ValueKind == JsonValueKind.String ? res.GetString()! : "Let me connect you with a specialist who can help you better.";

            return new AiEscalationResult { ShouldEscalate = escalate, Reason = reason, SuggestedResponse = response };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "AI escalation check failed ({BaseUrl}); using rule-based fallback.", _baseUrl);
            return await new RuleBasedAiFallback().ShouldEscalateAsync(customerMessage, conversationHistory, ticket, ct);
        }
    }

    private static string? Clamp(string? value, string[] allowed) =>
        value is null ? null : allowed.FirstOrDefault(a => a.Equals(value.Trim(), StringComparison.OrdinalIgnoreCase))
            ?? allowed[0];

    private static string? Truncate(string? s, int max) => s is null ? null : (s.Length <= max ? s : s[..max]);
}
