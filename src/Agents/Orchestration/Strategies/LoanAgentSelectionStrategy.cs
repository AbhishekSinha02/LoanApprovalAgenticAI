using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.Agents.Chat;
using Microsoft.SemanticKernel.ChatCompletion;

namespace LoanApproval.Agents.Orchestration.Strategies;

/// <summary>
/// Sequences agents in a fixed pipeline order:
/// DocumentAnalyst → CreditAnalyst → RiskAnalyst → ComplianceOfficer → LoanOfficer
/// </summary>
internal sealed class LoanAgentSelectionStrategy : SelectionStrategy
{
    internal static readonly string[] AgentPipeline =
    [
        AgentNames.DocumentAnalyst,
        AgentNames.CreditAnalyst,
        AgentNames.RiskAnalyst,
        AgentNames.ComplianceOfficer,
        AgentNames.LoanOfficer
    ];

    // Stateful counter: each BuildGroupChat() creates a new strategy instance,
    // so this counter is per-request and never shared across requests.
    private int _nextIndex;

    protected override Task<Agent> SelectAgentAsync(
        IReadOnlyList<Agent> agents,
        IReadOnlyList<ChatMessageContent> history,
        CancellationToken cancellationToken)
    {
        var nextName = AgentPipeline[_nextIndex % AgentPipeline.Length];
        _nextIndex++;

        var selected = agents.FirstOrDefault(a => a.Name == nextName)
            ?? throw new InvalidOperationException($"Agent '{nextName}' not found in the group chat.");

        return Task.FromResult(selected);
    }
}
