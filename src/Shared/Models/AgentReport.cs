namespace LoanApproval.Shared.Models;

public record AgentReport
{
    public required string AgentName { get; init; }
    public required string Summary { get; init; }
    public required bool Passed { get; init; }

    /// <summary>Composite score 0–100 from this agent's domain assessment.</summary>
    public required int Score { get; init; }
    public List<string> Findings { get; init; } = [];
    public List<string> Concerns { get; init; } = [];
    public Dictionary<string, object> Metadata { get; init; } = [];
    public DateTime CompletedAt { get; init; } = DateTime.UtcNow;
}
