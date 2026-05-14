using Azure.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.SemanticKernel;

namespace LoanApproval.Agents.Extensions;

public static class KernelExtensions
{
    public static IKernelBuilder AddAzureOpenAIOrLocal(this IKernelBuilder builder, IConfiguration config)
    {
        var useLocal = bool.TryParse(config["USE_LOCAL_INFRA"], out var flag) && flag;

        if (useLocal)
        {
            var ollamaModel    = config["Ollama:Model"]    ?? "llama3";
            var ollamaEndpoint = config["Ollama:Endpoint"] ?? "http://localhost:11434";
            builder.AddOpenAIChatCompletion(ollamaModel, new Uri($"{ollamaEndpoint}/v1"), apiKey: "ollama");
        }
        else
        {
            var endpoint       = config["AzureOpenAI:Endpoint"]
                ?? throw new InvalidOperationException("AzureOpenAI:Endpoint is required when USE_LOCAL_INFRA=false");
            var deploymentName = config["AzureOpenAI:DeploymentName"] ?? "gpt-4o";

            // Workload Identity (OIDC federated credential) — no API keys in code
            builder.AddAzureOpenAIChatCompletion(deploymentName, endpoint, new DefaultAzureCredential());
        }

        return builder;
    }
}
