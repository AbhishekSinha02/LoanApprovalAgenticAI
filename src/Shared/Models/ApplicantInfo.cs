namespace LoanApproval.Shared.Models;

public record ApplicantInfo
{
    public required string FirstName { get; init; }
    public required string LastName { get; init; }
    public string FullName => $"{FirstName} {LastName}";

    /// <summary>Store only a SHA-256 hash — never the raw SSN.</summary>
    public required string SocialSecurityNumberHash { get; init; }
    public required DateTime DateOfBirth { get; init; }
    public required Address Address { get; init; }
    public required EmploymentStatus EmploymentStatus { get; init; }
    public required decimal AnnualIncome { get; init; }
    public required decimal MonthlyDebtObligations { get; init; }
    public required int CreditScore { get; init; }
    public int YearsAtCurrentJob { get; init; }
    public string? EmployerName { get; init; }
    public int Age => DateTime.UtcNow.Year - DateOfBirth.Year;
}
