using LoanApproval.Shared.Models;

namespace LoanApproval.Shared.Interfaces;

public interface ILoanOrchestrator
{
    Task<LoanDecision> ProcessApplicationAsync(LoanApplication application, CancellationToken cancellationToken = default);
}
