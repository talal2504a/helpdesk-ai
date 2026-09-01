using HelpDesk.Application.DTOs.Messages;
using HelpDesk.Application.Interfaces;
using HelpDesk.Application.DTOs.Ai;
using HelpDesk.Domain.Entities;

namespace HelpDesk.Infrastructure.Ai;

public class RuleBasedAiFallback : IAiClient
{
    public bool IsConfigured => false;

    public static readonly string[] Categories = { "Bug Report", "Feature Request", "Billing Issue", "Account Access", "How To Question", "Other" };
    public static readonly string[] Priorities = { "Low", "Medium", "High", "Critical" };
    public static readonly string[] Sentiments = { "Positive", "Neutral", "Negative", "Angry" };

    private static readonly Dictionary<string, string[]> CategoryKeywords = new()
    {
        ["Bug Report"] = new[] { "bug", "error", "crash", "broken", "exception", "fail", "not working", "issue" },
        ["Feature Request"] = new[] { "feature", "add", "enhancement", "improve", "request", "would be nice" },
        ["Billing Issue"] = new[] { "billing", "invoice", "charge", "refund", "payment", "subscription", "price" },
        ["Account Access"] = new[] { "login", "password", "access", "locked out", "account", "sign in", "2fa", "otp" },
        ["How To Question"] = new[] { "how to", "how do i", "question", "guide", "configure", "setup", "help with" },
    };

    private static readonly string[][] PriorityKeywords =
    {
        new[] { "urgent", "critical", "down", "outage", "production down", "asap", "data loss", "security breach", "cannot work at all" },
        new[] { "high priority", "important", "blocked", "deadline", "today" },
        new[] { "whenever possible", "minor", "slow" },
    };

    private static readonly string[] HumanRequestPatterns = new[]
    {
        "where is the agent", "where is your support agent", "i want a real person",
        "connect me to an agent", "i don't want to talk to ai", "give me a human",
        "i want to speak to a human", "i need a human", "human support", "real agent",
        "talk to a person", "speak to agent", "customer service", "live agent",
        "where is the human", "not ai", "not bot"
    };

    public System.Threading.Tasks.Task<AIAnalysis> AnalyzeAsync(string title, string description, CancellationToken ct = default)
    {
        return System.Threading.Tasks.Task.FromResult(Build(title, description));
    }

    public System.Threading.Tasks.Task<string> GenerateReplyAsync(Ticket ticket, int customerId, string customerMessage, IReadOnlyList<MessageDto> conversationHistory, CancellationToken ct = default)
    {
        if (ticket.Status.Name == "Closed" || !ticket.AiModeEnabled)
            return System.Threading.Tasks.Task.FromResult(string.Empty);

        var lowerMessage = customerMessage.ToLowerInvariant();
        foreach (var pattern in HumanRequestPatterns)
        {
            if (lowerMessage.Contains(pattern))
            {
                var alreadyEscalated = ticket.WaitingForAgent;
                var humanReply = alreadyEscalated
                    ? "Your request has already been transferred to a human support agent. An agent will continue this conversation shortly."
                    : "I understand you'd like to speak with a human agent. Let me connect you right away. Please hold for a moment.";
                return System.Threading.Tasks.Task.FromResult(humanReply);
            }
        }

        var category = Categories[0];
        var text = $"{ticket.Title} {customerMessage}".ToLowerInvariant();
        foreach (var (name, words) in CategoryKeywords)
        {
            if (words.Any(text.Contains)) { category = name; break; }
        }

        var lastCustomerMsg = conversationHistory.LastOrDefault(m => m.SenderRole == "Customer")?.Body ?? customerMessage;
        var aiReply = category switch
        {
            "Billing Issue" => "Hi there, I see you have a billing concern. Could you please share the order number or invoice ID so I can look into this for you? Our billing team will assist you once we have the details.",
            "Account Access" => "I understand you're having trouble with account access. Could you confirm which email is associated with your account? This will help us verify your identity and assist you better.",
            "Bug Report" => "Thank you for reporting this issue. Could you please share any screenshots or error messages that might help us reproduce the issue? Also, let us know which browser and device you're using.",
            "Feature Request" => "Thank you for your suggestion! We appreciate your feedback. I'll make sure this is passed along to our product team. Is there anything else I can help you with in the meantime?",
            "How To Question" => "I'd be happy to help you with that. Let me walk you through the steps. First, could you tell me which specific part you're finding difficult?",
            _ => "Hi, thanks for your message. Could you please provide more details about your issue so I can assist you better?"
        };

        return System.Threading.Tasks.Task.FromResult(aiReply);
    }

    public System.Threading.Tasks.Task<string> GenerateAutoReplyAsync(string ticketTitle, string customerMessage, CancellationToken ct = default)
    {
        var text = $"{ticketTitle} {customerMessage}".ToLowerInvariant();
        string acknowledgement;

        if (text.Contains("login") || text.Contains("password") || text.Contains("access") || text.Contains("account"))
            acknowledgement = "We understand you're having trouble accessing your account. Our team is looking into this and will help you regain access shortly.";
        else if (text.Contains("error") || text.Contains("bug") || text.Contains("crash") || text.Contains("broken"))
            acknowledgement = "Thank you for reporting this issue. Our technical team is reviewing the details and will work on a fix for you.";
        else if (text.Contains("billing") || text.Contains("payment") || text.Contains("charge") || text.Contains("invoice"))
            acknowledgement = "We see your billing concern. Our billing specialist will review this and get back to you with a resolution shortly.";
        else if (text.Contains("urgent") || text.Contains("critical") || text.Contains("down") || text.Contains("asap"))
            acknowledgement = "We understand this is urgent. Our team is treating this as a priority and will respond to you within minutes.";
        else
            acknowledgement = "Thank you for reaching out. We have received your message and our team will get back to you shortly with a helpful response.";

        var reply = $"{acknowledgement} We appreciate your patience and will update you as soon as possible.";
        return System.Threading.Tasks.Task.FromResult(reply);
    }

    public System.Threading.Tasks.Task<AiEscalationResult> ShouldEscalateAsync(string customerMessage, IReadOnlyList<MessageDto> conversationHistory, Ticket ticket, CancellationToken ct = default)
    {
        var lowerMessage = customerMessage.ToLowerInvariant();

        foreach (var pattern in HumanRequestPatterns)
        {
            if (lowerMessage.Contains(pattern))
            {
                var alreadyEscalated = ticket.WaitingForAgent;
                return System.Threading.Tasks.Task.FromResult(new AiEscalationResult
                {
                    ShouldEscalate = true,
                    Reason = "Customer requested human agent",
                    SuggestedResponse = alreadyEscalated
                        ? "Your request has already been transferred to a human support agent. An agent will continue this conversation shortly."
                        : "I understand you'd like to speak with a human agent. Let me connect you right away. Please hold for a moment."
                });
            }
        }

        var escalatedCount = conversationHistory.Count(m => m.IsAiGenerated);
        var customerMsgs = conversationHistory.Count(m => m.SenderRole == "Customer");
        if (escalatedCount > 0 && customerMsgs > 6)
        {
            return System.Threading.Tasks.Task.FromResult(new AiEscalationResult
            {
                ShouldEscalate = true,
                Reason = "Extended conversation without resolution",
                SuggestedResponse = "I want to make sure you get the best help possible. Let me connect you with a specialist who can assist you further."
            });
        }

        var sensitiveWords = new[] { "refund", "legal", "lawsuit", "lawyer", "attorney", "cancel subscription", "delete account", "fraud", "scam" };
        if (sensitiveWords.Any(lowerMessage.Contains))
        {
            return System.Threading.Tasks.Task.FromResult(new AiEscalationResult
            {
                ShouldEscalate = true,
                Reason = "Sensitive topic detected",
                SuggestedResponse = "This matter requires special attention. Let me connect you with a senior specialist who can handle this for you."
            });
        }

        return System.Threading.Tasks.Task.FromResult(new AiEscalationResult
        {
            ShouldEscalate = false,
            Reason = "No escalation needed",
            SuggestedResponse = string.Empty
        });
    }

    public AIAnalysis Build(string title, string description)
    {
        var text = $"{title} {description}".ToLowerInvariant();

        string? category = null;
        int bestHits = 0;
        foreach (var (name, words) in CategoryKeywords)
        {
            var hits = words.Count(text.Contains);
            if (hits > bestHits) { bestHits = hits; category = name; }
        }
        category ??= "Other";

        var priority = "Low";
        if (PriorityKeywords[0].Any(text.Contains)) priority = "Critical";
        else if (PriorityKeywords[1].Any(text.Contains)) priority = "High";
        else if (bestHits >= 2) priority = "Medium";

        var negativeWords = new[] { "angry", "frustrated", "terrible", "worst", "unacceptable", "disappointed", "furious" };
        var positiveWords = new[] { "thanks", "thank you", "great", "appreciate", "excellent", "love" };
        var sentiment = positiveWords.Any(text.Contains) ? "Positive"
            : negativeWords.Any(text.Contains) ? "Angry"
            : text.Contains("not working") || text.Contains("broken") || text.Contains("error") ? "Negative"
            : "Neutral";

        var firstSentence = description.Split('.', '!', '\n')[0].Trim();
        var summary = $"[{category}/{priority}] {title}" + (firstSentence.Length > 0 ? $". {firstSentence}".Replace("\r", "") : "");

        return new AIAnalysis
        {
            Category = category,
            Priority = priority,
            Summary = summary.Length > 2000 ? summary[..2000] : summary,
            Sentiment = sentiment,
            SuggestedReply =
                $"Hi,\n\nThanks for reaching out about \"{title}\". We have logged this as a {priority.ToLowerInvariant()}-priority " +
                $"{category.ToLowerInvariant()} ticket. Our team is reviewing the details and will get back to you shortly.\n\n" +
                "Could you please share any screenshots or error messages that might help us reproduce the issue?\n\nBest regards,\nSupport Team",
            ConfidenceScore = 0.45,
            Provider = "RuleBased"
        };
    }
}
