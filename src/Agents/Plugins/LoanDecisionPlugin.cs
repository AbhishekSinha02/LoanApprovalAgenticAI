using System.ComponentModel;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;

namespace LoanApproval.Agents.Plugins;

public sealed class LoanDecisionPlugin(ILogger<LoanDecisionPlugin> logger)
{
    [KernelFunction, Description("Compute a weighted overall score from individual agent scores.")]
    public string ComputeOverallScore(
        [Description("Document analysis score (0–100)")] int documentScore,
        [Description("Credit assessment score (0–100)")] int creditScore,
        [Description("Risk assessment score (0–100, lower = less risky)")] int riskScore,
        [Description("Compliance score (0–100)")] int complianceScore)
    {
        logger.LogDebug("[PLUGIN:Decision] ComputeOverallScore ← doc={Doc}, credit={Credit}, risk={Risk}, compliance={Compliance}",
            documentScore, creditScore, riskScore, complianceScore);

        // Convert risk score so higher = better, then weight each agent
        var invertedRisk = 100 - riskScore;

        var weightedScore = (int)Math.Round(
            documentScore   * 0.20 +
            creditScore     * 0.35 +
            invertedRisk    * 0.30 +
            complianceScore * 0.15);

        var result = JsonSerializer.Serialize(new
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
        logger.LogDebug("[PLUGIN:Decision] ComputeOverallScore → {Result}", result);
        return result;
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
        logger.LogDebug("[PLUGIN:Decision] DetermineDecisionStatus ← overallScore={Score}, credit={Credit}, dti={DTI}%, compliance={Comp}, kyc={KYC}, risk={Risk}",
            overallScore, creditScore, dtiRatio, compliancePass, kycPass, riskLevel);

        // Hard denials — policy floors that override the score
        if (!kycPass)
        {
            var r = JsonSerializer.Serialize(new
            {
                status = "Rejected",
                reason = "KYC/identity verification failed — mandatory policy requirement",
                hardDenial = true
            });
            logger.LogWarning("[PLUGIN:Decision] Hard denial — KYC failed");
            logger.LogDebug("[PLUGIN:Decision] DetermineDecisionStatus → {Result}", r);
            return r;
        }

        if (!compliancePass)
        {
            var r = JsonSerializer.Serialize(new
            {
                status = "Rejected",
                reason = "Compliance check failed — regulatory requirement not met",
                hardDenial = true
            });
            logger.LogWarning("[PLUGIN:Decision] Hard denial — Compliance failed");
            logger.LogDebug("[PLUGIN:Decision] DetermineDecisionStatus → {Result}", r);
            return r;
        }

        if (creditScore < 500)
        {
            var r = JsonSerializer.Serialize(new
            {
                status = "Rejected",
                reason = $"Credit score {creditScore} is below the 500-point policy minimum",
                hardDenial = true
            });
            logger.LogWarning("[PLUGIN:Decision] Hard denial — credit score {Score} < 500", creditScore);
            logger.LogDebug("[PLUGIN:Decision] DetermineDecisionStatus → {Result}", r);
            return r;
        }

        if (dtiRatio > 50)
        {
            var r = JsonSerializer.Serialize(new
            {
                status = "Rejected",
                reason = $"DTI ratio {dtiRatio:F1}% exceeds the 50% hard ceiling",
                hardDenial = true
            });
            logger.LogWarning("[PLUGIN:Decision] Hard denial — DTI {DTI}% > 50%", dtiRatio);
            logger.LogDebug("[PLUGIN:Decision] DetermineDecisionStatus → {Result}", r);
            return r;
        }

        // Score-based decision
        var (status, conditions) = (overallScore, riskLevel) switch
        {
            (>= 75, "Low" or "Medium") => ("Approved", new List<string>()),
            (>= 60, _) => ("ConditionallyApproved", new List<string> { "Provide 2 years of tax returns", "Additional income verification may be required" }),
            (>= 45, _) => ("ConditionallyApproved", new List<string> { "Co-signer required", "Reduced loan amount may be offered", "Higher interest rate applies" }),
            _ => ("Rejected", new List<string> { "Score below minimum approval threshold" })
        };

        var result = JsonSerializer.Serialize(new { status, conditions, overallScore, hardDenial = false });
        logger.LogInformation("[PLUGIN:Decision] DetermineDecisionStatus → status={Status}, score={Score}", status, overallScore);
        logger.LogDebug("[PLUGIN:Decision] DetermineDecisionStatus → {Result}", result);
        return result;
    }

    [KernelFunction, Description("Calculate the approved loan amount, potentially adjusted from the requested amount.")]
    public string CalculateApprovedAmount(
        [Description("Requested loan amount")] decimal requestedAmount,
        [Description("Overall weighted score (0–100)")] int overallScore,
        [Description("Debt-to-income ratio as a percentage")] decimal dtiRatio,
        [Description("Monthly income")] decimal monthlyIncome)
    {
        logger.LogDebug("[PLUGIN:Decision] CalculateApprovedAmount ← requested={Requested}, score={Score}, dti={DTI}%, monthlyIncome={Income}",
            requestedAmount, overallScore, dtiRatio, monthlyIncome);

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

        var result = JsonSerializer.Serialize(new
        {
            requestedAmount,
            approvedAmount,
            adjustmentApplied = approvedAmount < requestedAmount,
            maxMonthlyPayment = Math.Round(maxPayment, 2)
        });
        logger.LogDebug("[PLUGIN:Decision] CalculateApprovedAmount → {Result}", result);
        return result;
    }
}
