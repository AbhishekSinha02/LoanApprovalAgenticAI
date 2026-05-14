using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.Agents.Chat;
using Microsoft.SemanticKernel.ChatCompletion;

namespace LoanApproval.Agents.Orchestration.Strategies;

/// <summary>
/// Terminates the group chat once the LoanOfficer (Decision Agent) has responded.
/// Falls back to MaximumIterations (set in ExecutionSettings) as a safety net.
/// </summary>
internal sealed class LoanTerminationStrategy : TerminationStrategy
{
    protected override Task<bool> ShouldAgentTerminateAsync(
        Agent agent,
        IReadOnlyList<ChatMessageContent> history,
        CancellationToken cancellationToken)
        => Task.FromResult(agent.Name == AgentNames.LoanOfficer);
}
