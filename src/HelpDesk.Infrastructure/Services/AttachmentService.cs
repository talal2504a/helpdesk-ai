using HelpDesk.Application.DTOs.Attachments;
using HelpDesk.Application.Exceptions;
using HelpDesk.Application.Interfaces;
using HelpDesk.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace HelpDesk.Infrastructure.Services;

public class AttachmentService : IAttachmentService
{
    private readonly Data.HelpDeskDbContext _db;
    private readonly IFileStorage _storage;

    public AttachmentService(Data.HelpDeskDbContext db, IFileStorage storage)
    {
        _db = db; _storage = storage;
    }

    public async Task<UploadAttachmentsResponse> UploadForTicketAsync(int ticketId, IReadOnlyList<FileUploadDto> files, ICurrentUser uploader, CancellationToken ct = default)
    {
        if (files.Count == 0) throw new ValidationException("files", "No files were provided.");
        if (files.Count > 5) throw new ValidationException("files", "A maximum of 5 files can be uploaded at once.");

        var ticket = await _db.Tickets.FirstOrDefaultAsync(t => t.Id == ticketId, ct)
                     ?? throw new NotFoundException("Ticket");
        if (!uploader.IsStaffView() && ticket.UserId != uploader.UserId)
            throw new ForbiddenException("You can only upload files to your own tickets.");

        var result = new List<AttachmentDto>(files.Count);
        foreach (var f in files)
        {
            var url = await _storage.SaveAsync(ticketId, f, ct);
            var entity = new Attachment
            {
                TicketId = ticketId,
                FileName = SanitizeFileName(f.FileName),
                FileUrl = url,
                FileType = string.IsNullOrWhiteSpace(f.ContentType) ? "application/octet-stream" : f.ContentType,
                FileSize = f.Content.Length,
                UploadedBy = uploader.UserId!.Value,
                UploadedAt = DateTime.UtcNow
            };
            _db.Attachments.Add(entity);
            result.Add(new AttachmentDto(entity.Id, entity.TicketId, null, entity.FileName, url, entity.FileType,
                entity.FileSize, uploader.UserId!.Value, "", entity.UploadedAt));
        }

        ticket.UpdatedAt = DateTime.UtcNow;
        _db.TicketHistory.Add(new TicketHistory
        {
            TicketId = ticketId, UserId = uploader.UserId!.Value,
            Action = Domain.Entities.TicketActions.AttachmentAdded,
            NewValue = string.Join(", ", result.Select(r => r.FileName)),
            CreatedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync(ct);

        var names = await _db.Users.Where(u => u.Id == uploader.UserId).Select(u => u.Name).FirstAsync(ct);
        return new UploadAttachmentsResponse(result
            .Select(r => r with { UploaderName = names })
            .ToList());
    }

    private static string SanitizeFileName(string name)
    {
        var clean = Path.GetFileName(name.Replace('\\', '/'));
        return string.IsNullOrWhiteSpace(clean) ? "file" : (clean.Length > 250 ? clean[^250..] : clean);
    }
}