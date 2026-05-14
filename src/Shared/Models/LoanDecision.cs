namespace LoanApproval.Shared.Models;

public record LoanDecision
{
    public required Guid ApplicationId { get; init; }
    public required DecisionStatus Status { get; init; }
    public required string Reasoning { get; init; }
    public decimal? ApprovedAmount { get; init; }
    public decimal? InterestRate { get; init; }
    public int? TermMonths { get; init; }
    public List<string> Conditions { get; init; } = [];
    public List<AgentReport> AgentReports { get; init; } = [];
    public required int OverallScore { get; init; }
    public required RiskLevel RiskLevel { get; init; }
    public DateTime DecidedAt { get; init; } = DateTime.UtcNow;
}
