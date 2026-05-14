using LoanApproval.Agents.Extensions;
using LoanApproval.Agents.Orchestration;
using LoanApproval.Agents.Plugins;
using LoanApproval.Shared.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;

namespace LoanApproval.Agents;

public static class AgentServiceExtensions
{
    public static IServiceCollection AddLoanApprovalAgents(this IServiceCollection services, IConfiguration config)
    {
        services.AddSingleton(_ =>
        {
            var builder = Kernel.CreateBuilder();
            builder.AddAzureOpenAIOrLocal(config);
            return builder.Build();
        });

        services.AddSingleton<DocumentAnalysisPlugin>();
        services.AddSingleton<CreditAssessmentPlugin>();
        services.AddSingleton<RiskAssessmentPlugin>();
        services.AddSingleton<CompliancePlugin>();
        services.AddSingleton<LoanDecisionPlugin>();
        services.AddSingleton<ILoanOrchestrator, LoanApprovalOrchestrator>();

        return services;
    }
}
