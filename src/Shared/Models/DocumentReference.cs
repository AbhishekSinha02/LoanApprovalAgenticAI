namespace LoanApproval.Shared.Models;

public record DocumentReference
{
    public required Guid DocumentId { get; init; }
    public required DocumentType Type { get; init; }
    public required string StorageUri { get; init; }
    public string? FileName { get; init; }
    public DateTime UploadedAt { get; init; } = DateTime.UtcNow;
}
