namespace HelpDesk.Application.DTOs.Ai;

public class AiEscalationResult
{
    public bool ShouldEscalate { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string SuggestedResponse { get; set; } = string.Empty;
}
