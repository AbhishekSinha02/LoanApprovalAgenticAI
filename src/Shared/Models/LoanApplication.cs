namespace LoanApproval.Shared.Models;

public record LoanApplication
{
    public Guid ApplicationId { get; init; } = Guid.NewGuid();
    public required ApplicantInfo Applicant { get; init; }
    public required LoanDetails LoanDetails { get; init; }
    public List<DocumentReference> Documents { get; init; } = [];
    public DateTime SubmittedAt { get; init; } = DateTime.UtcNow;
    public string? TenantId { get; init; }
    public string? ReferenceNumber { get; init; }
}
