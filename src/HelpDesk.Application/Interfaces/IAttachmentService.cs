using HelpDesk.Application.DTOs.Attachments;

namespace HelpDesk.Application.Interfaces;

/// <summary>Transport-agnostic file upload descriptor (keeps IFormFile out of the Application layer).</summary>
public record FileUploadDto(string FileName, string ContentType, Stream Content);

public interface IFileStorage
{
    /// <summary>Stores the file and returns its public URL path (e.g. /uploads/{ticketId}/{file}).</summary>
    Task<string> SaveAsync(int ticketId, FileUploadDto file, CancellationToken ct = default);
}

public interface IAttachmentService
{
    Task<UploadAttachmentsResponse> UploadForTicketAsync(int ticketId, IReadOnlyList<FileUploadDto> files, ICurrentUser uploader, CancellationToken ct = default);
}