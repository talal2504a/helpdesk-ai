using HelpDesk.Application.Interfaces;

namespace HelpDesk.Infrastructure.Storage;

/// <summary>Saves uploads under Storage__AttachmentsPath (default ./uploads). Azure: swap for Blob storage impl.</summary>
public class LocalFileStorage : IFileStorage
{
    private readonly string _root;
    private static readonly string[] AllowedExtensions =
        { ".png", ".jpg", ".jpeg", ".gif", ".pdf", ".txt", ".doc", ".docx", ".xls", ".xlsx", ".csv", ".zip", ".log" };
    public const long MaxFileSizeBytes = 10 * 1024 * 1024; // 10 MB

    public LocalFileStorage()
    {
        _root = Path.GetFullPath(Environment.GetEnvironmentVariable("Storage__AttachmentsPath") ?? "uploads");
        Directory.CreateDirectory(_root);
    }

    public async Task<string> SaveAsync(int ticketId, FileUploadDto file, CancellationToken ct = default)
    {
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!AllowedExtensions.Contains(ext))
            throw new ValidationException(
                $"file", $"File type '{ext}' is not allowed. Allowed: {string.Join(", ", AllowedExtensions)}");

        var dir = Path.Combine(_root, ticketId.ToString());
        Directory.CreateDirectory(dir);

        var storedName = $"{Guid.NewGuid():N}{ext}";
        var fullPath = Path.Combine(dir, storedName);

        await using var fs = File.Create(fullPath);
        var buffer = new byte[81920];
        long total = 0;
        int read;
        while ((read = await file.Content.ReadAsync(buffer, ct)) > 0)
        {
            total += read;
            if (total > MaxFileSizeBytes)
            {
                fs.Close();
                File.Delete(fullPath);
                throw new ValidationException("file", $"Each file must be at most {MaxFileSizeBytes / 1024 / 1024} MB.");
            }
            await fs.WriteAsync(buffer.AsMemory(0, read), ct);
        }

        return $"/uploads/{ticketId}/{storedName}";
    }
}