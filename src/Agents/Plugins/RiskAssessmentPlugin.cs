using System.ComponentModel;
using System.Text.Json;
using Microsoft.SemanticKernel;

namespace LoanApproval.Agents.Plugins;

public sealed class RiskAssessmentPlugin
{
    [KernelFunction, Description("Calculate a composite risk score (0–100, lower = less risky) from key applicant metrics.")]
    public string CalculateRiskScore(
        [Description("FICO credit score (300–850)")] int creditScore,
        [Description("Debt-to-income ratio as a percentage")] decimal dtiRatio,
        [Description("Years at current employer")] int yearsEmployed,
        [Description("Loan type (e.g. Mortgage, PersonalLoan, AutoLoan)")] string loanType,
        [Description("Loan-to-value ratio as a percentage; 0 if no collateral")] decimal ltvRatio)
    {
        // Credit score component (weight 40%)
        var creditComponent = creditScore switch
        {
            >= 800 => 0,
            >= 740 => 10,
            >= 670 => 20,
            >= 580 => 40,
            >= 500 => 60,
            _ => 80
        };

        // DTI component (weight 30%)
        var dtiComponent = dtiRatio switch
        {
            < 28 => 0,
            < 36 => 15,
            < 43 => 30,
            < 50 => 55,
            _ => 75
        };

        // Employment stability (weight 15%)
        var employmentComponent = yearsEmployed switch
        {
            >= 5 => 0,
            >= 3 => 15,
            >= 1 => 30,
            _ => 50
        };

        // LTV component for secured loans (weight 15%)
        var ltvComponent = loanType.ToLower() is "mortgage" or "homeequityloan" or "autoloan"
            ? ltvRatio switch
            {
                <= 60 => 0,
                <= 80 => 15,
                <= 95 => 35,
                _ => 60
            }
            : 20; // unsecured = moderate default risk weight

        var rawScore = (creditComponent * 0.40)
                     + (dtiComponent * 0.30)
                     + (employmentComponent * 0.15)
                     + (ltvComponent * 0.15);

        var riskScore = (int)Math.Round(Math.Clamp(rawScore, 0, 100));

        var riskLevel = riskScore switch
        {
            < 25 => "Low",
            < 50 => "Medium",
            < 70 => "High",
            _ => "VeryHigh"
        };

        return JsonSerializer.Serialize(new
        {
            riskScore,
            riskLevel,
            components = new { creditComponent, dtiComponent, employmentComponent, ltvComponent }
        });
    }

    [KernelFunction, Description("Determine the suggested interest rate based on the risk profile.")]
    public string DetermineInterestRate(
        [Description("Risk score (0–100)")] int riskScore,
        [Description("Loan type")] string loanType,
        [Description("Loan term in months")] int termMonths)
    {
        // Base rates by loan type (approximate current market rates)
        var baseRate = loanType.ToLower() switch
        {
            "mortgage" => 6.5m,
            "homeequityloan" => 7.0m,
            "autoloan" => 5.5m,
            "studentloan" => 5.0m,
            "businessloan" => 7.5m,
            _ => 8.0m  // PersonalLoan default
        };

        // Risk premium
        var riskPremium = riskScore switch
        {
            < 25 => 0m,
            < 50 => 0.5m,
            < 70 => 1.5m,
            _ => 3.0m
        };

        // Term adjustment (longer term = slightly higher rate)
        var termAdjustment = termMonths > 120 ? 0.25m : 0m;

        var finalRate = Math.Round(baseRate + riskPremium + termAdjustment, 2);

        return JsonSerializer.Serialize(new
        {
            suggestedRate = finalRate,
            baseRate,
            riskPremium,
            termAdjustment
        });
    }

    [KernelFunction, Description("Evaluate the loan-to-value ratio for secured loans.")]
    public string EvaluateLoanToValue(
        [Description("Requested loan amount")] decimal loanAmount,
        [Description("Appraised collateral value; 0 for unsecured loans")] decimal collateralValue,
        [Description("Loan type")] string loanType)
    {
        if (collateralValue <= 0)
            return JsonSerializer.Serialize(new
            {
                ltvRatio = 0,
                assessment = "Unsecured loan — no collateral",
                pass = true
            });

        var ltvRatio = Math.Round(loanAmount / collateralValue * 100, 2);
        var maxLtv = loanType.ToLower() switch
        {
            "mortgage" => 97m,
            "homeequityloan" => 85m,
            "autoloan" => 110m,
            _ => 80m
        };

        return JsonSerializer.Serialize(new
        {
            ltvRatio,
            maxAllowedLtv = maxLtv,
            pass = ltvRatio <= maxLtv,
            assessment = ltvRatio <= maxLtv
                ? $"LTV {ltvRatio}% is within the {maxLtv}% limit"
                : $"LTV {ltvRatio}% exceeds the {maxLtv}% maximum — loan amount must be reduced"
        });
    }
}
