namespace HelpDesk.Application.DTOs.Attachments;

public record AttachmentDto(int Id, int TicketId, int? MessageId, string FileName, string FileUrl,
    string FileType, long FileSize, int UploadedBy, string UploaderName, DateTime UploadedAt);

public record UploadAttachmentsResponse(IReadOnlyList<AttachmentDto> Attachments);