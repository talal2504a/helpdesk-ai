using HelpDesk.Application.DTOs.Attachments;

namespace HelpDesk.Application.DTOs.Messages;

public record MessageDto(int Id, int TicketId, int SenderId, string SenderName, string SenderRole,
    string Body, DateTime CreatedAt, bool IsInternal, bool IsAiGenerated, IReadOnlyList<AttachmentDto> Attachments);

public record SendMessageRequest(string Body, bool IsInternal = false);