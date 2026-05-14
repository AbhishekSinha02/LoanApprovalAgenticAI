namespace LoanApproval.Shared.Models;

public record LoanDetails
{
    public required LoanType LoanType { get; init; }
    public required decimal RequestedAmount { get; init; }
    public required int TermMonths { get; init; }
    public required string Purpose { get; init; }
    public decimal? CollateralValue { get; init; }
    public string? CollateralDescription { get; init; }
    public Address? PropertyAddress { get; init; }
}
