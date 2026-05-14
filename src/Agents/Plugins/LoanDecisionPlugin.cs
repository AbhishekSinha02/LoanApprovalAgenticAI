using System.ComponentModel;
using System.Text.Json;
using Microsoft.SemanticKernel;

namespace LoanApproval.Agents.Plugins;

public sealed class LoanDecisionPlugin
{
    [KernelFunction, Description("Compute a weighted overall score from individual agent scores.")]
    public string ComputeOverallScore(
        [Description("Document analysis score (0–100)")] int documentScore,
        [Description("Credit assessment score (0–100)")] int creditScore,
        [Description("Risk assessment score (0–100, lower = less risky)")] int riskScore,
        [Description("Compliance score (0–100)")] int complianceScore)
    {
        // Convert risk score so higher = better, then weight each agent
        var invertedRisk = 100 - riskScore;

        var weightedScore = (int)Math.Round(
            documentScore   * 0.20 +
            creditScore     * 0.35 +
            invertedRisk    * 0.30 +
            complianceScore * 0.15);

        return JsonSerializer.Serialize(new
        {
            overallScore = weightedScore,
            breakdown = new
            {
                documentScore,
                creditScore,
                riskScoreInverted = invertedRisk,
                complianceScore
            }
        });
    }

    [KernelFunction, Description("Determine the final loan decision status from key underwriting metrics.")]
    public string DetermineDecisionStatus(
        [Description("Overall weighted score (0–100)")] int overallScore,
        [Description("FICO credit score")] int creditScore,
        [Description("Debt-to-income ratio as a percentage")] decimal dtiRatio,
        [Description("Whether all compliance checks passed")] bool compliancePass,
        [Description("Whether identity/KYC checks passed")] bool kycPass,
        [Description("Risk level: Low, Medium, High, VeryHigh")] string riskLevel)
    {
        // Hard denials — policy floors that override the score
        if (!kycPass)
            return JsonSerializer.Serialize(new
            {
                status = "Rejected",
                reason = "KYC/identity verification failed — mandatory policy requirement",
                hardDenial = true
            });

        if (!compliancePass)
            return JsonSerializer.Serialize(new
            {
                status = "Rejected",
                reason = "Compliance check failed — regulatory requirement not met",
                hardDenial = true
            });

        if (creditScore < 500)
            return JsonSerializer.Serialize(new
            {
                status = "Rejected",
                reason = $"Credit score {creditScore} is below the 500-point policy minimum",
                hardDenial = true
            });

        if (dtiRatio > 50)
            return JsonSerializer.Serialize(new
            {
                status = "Rejected",
                reason = $"DTI ratio {dtiRatio:F1}% exceeds the 50% hard ceiling",
                hardDenial = true
            });

        // Score-based decision
        var (status, conditions) = (overallScore, riskLevel) switch
        {
            (>= 75, "Low" or "Medium") => ("Approved", new List<string>()),
            (>= 60, _) => ("ConditionallyApproved", new List<string> { "Provide 2 years of tax returns", "Additional income verification may be required" }),
            (>= 45, _) => ("ConditionallyApproved", new List<string> { "Co-signer required", "Reduced loan amount may be offered", "Higher interest rate applies" }),
            _ => ("Rejected", new List<string> { "Score below minimum approval threshold" })
        };

        return JsonSerializer.Serialize(new { status, conditions, overallScore, hardDenial = false });
    }

    [KernelFunction, Description("Calculate the approved loan amount, potentially adjusted from the requested amount.")]
    public string CalculateApprovedAmount(
        [Description("Requested loan amount")] decimal requestedAmount,
        [Description("Overall weighted score (0–100)")] int overallScore,
        [Description("Debt-to-income ratio as a percentage")] decimal dtiRatio,
        [Description("Monthly income")] decimal monthlyIncome)
    {
        // Maximum monthly payment the applicant can safely carry
        var maxDtiAllowed = 43m;
        var maxPayment = monthlyIncome * (maxDtiAllowed / 100m);

        // Full amount if score is strong
        var approvedAmount = overallScore switch
        {
            >= 75 => requestedAmount,
            >= 60 => requestedAmount * 0.90m,
            >= 45 => requestedAmount * 0.75m,
            _ => 0m
        };

        approvedAmount = Math.Round(approvedAmount, 2);

        return JsonSerializer.Serialize(new
        {
            requestedAmount,
            approvedAmount,
            adjustmentApplied = approvedAmount < requestedAmount,
            maxMonthlyPayment = Math.Round(maxPayment, 2)
        });
    }
}
