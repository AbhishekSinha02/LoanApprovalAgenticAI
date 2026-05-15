using System.ComponentModel;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;

namespace LoanApproval.Agents.Plugins;

public sealed class CreditAssessmentPlugin(ILogger<CreditAssessmentPlugin> logger)
{
    [KernelFunction, Description("Calculate monthly payment for a loan.")]
    public string CalculateMonthlyPayment(
        [Description("Principal loan amount")] decimal principal,
        [Description("Annual interest rate as a percentage (e.g. 6.5)")] decimal annualRatePercent,
        [Description("Loan term in months")] int termMonths)
    {
        logger.LogDebug("[PLUGIN:Credit] CalculateMonthlyPayment ← principal={Principal}, rate={Rate}%, term={Term}mo",
            principal, annualRatePercent, termMonths);

        if (annualRatePercent <= 0)
        {
            var flat = Math.Round(principal / termMonths, 2);
            var result0 = JsonSerializer.Serialize(new { monthlyPayment = flat, totalCost = flat * termMonths });
            logger.LogDebug("[PLUGIN:Credit] CalculateMonthlyPayment → {Result}", result0);
            return result0;
        }

        var monthlyRate = annualRatePercent / 100m / 12m;
        var factor = (double)(monthlyRate * (decimal)Math.Pow(1 + (double)monthlyRate, termMonths)
                    / ((decimal)Math.Pow(1 + (double)monthlyRate, termMonths) - 1));
        var payment = Math.Round((decimal)factor * principal, 2);

        var result = JsonSerializer.Serialize(new
        {
            monthlyPayment = payment,
            totalCost = Math.Round(payment * termMonths, 2),
            totalInterest = Math.Round(payment * termMonths - principal, 2)
        });
        logger.LogDebug("[PLUGIN:Credit] CalculateMonthlyPayment → {Result}", result);
        return result;
    }

    [KernelFunction, Description("Calculate debt-to-income ratio and assess its risk.")]
    public string CalculateDebtToIncomeRatio(
        [Description("Gross monthly income")] decimal monthlyIncome,
        [Description("Existing monthly debt obligations")] decimal existingMonthlyDebt,
        [Description("Proposed new monthly loan payment")] decimal proposedPayment)
    {
        logger.LogDebug("[PLUGIN:Credit] CalculateDebtToIncomeRatio ← monthlyIncome={Income}, existingDebt={Existing}, newPayment={New}",
            monthlyIncome, existingMonthlyDebt, proposedPayment);

        if (monthlyIncome <= 0)
            return JsonSerializer.Serialize(new { error = "Monthly income must be positive." });

        var totalDebt = existingMonthlyDebt + proposedPayment;
        var dtiRatio = Math.Round(totalDebt / monthlyIncome * 100, 2);

        var assessment = dtiRatio switch
        {
            < 28 => "Excellent — well within standard guidelines",
            < 36 => "Good — meets conventional loan guidelines",
            < 43 => "Acceptable — meets FHA/VA guidelines but higher risk",
            < 50 => "High — exceeds preferred limits; increased scrutiny required",
            _ => "Very High — exceeds safe lending thresholds"
        };

        var pass = dtiRatio < 43;

        var result = JsonSerializer.Serialize(new { dtiRatio, assessment, pass, totalMonthlyDebt = totalDebt });
        logger.LogDebug("[PLUGIN:Credit] CalculateDebtToIncomeRatio → {Result}", result);
        return result;
    }

    [KernelFunction, Description("Evaluate a credit score and return a risk category with recommendation.")]
    public string EvaluateCreditScore(
        [Description("FICO credit score (300–850)")] int creditScore)
    {
        logger.LogDebug("[PLUGIN:Credit] EvaluateCreditScore ← creditScore={Score}", creditScore);
        var (category, recommendation, riskPremium) = creditScore switch
        {
            >= 800 => ("Exceptional", "Approve — lowest rate tier", 0m),
            >= 740 => ("Very Good", "Approve — standard rate", 0.25m),
            >= 670 => ("Good", "Approve — slight rate adjustment", 0.75m),
            >= 580 => ("Fair", "Conditional — higher rate, possible co-signer", 2.0m),
            >= 500 => ("Poor", "High risk — significant rate premium or decline", 4.0m),
            _ => ("Very Poor", "Decline — does not meet minimum credit requirements", 0m)
        };

        var result = JsonSerializer.Serialize(new
        {
            creditScore,
            category,
            recommendation,
            riskPremiumPercent = riskPremium,
            meetsMinimum = creditScore >= 580
        });
        logger.LogDebug("[PLUGIN:Credit] EvaluateCreditScore → {Result}", result);
        return result;
    }

    [KernelFunction, Description("Assess loan affordability relative to income and requested amount.")]
    public string AssessAffordability(
        [Description("Annual gross income")] decimal annualIncome,
        [Description("Requested loan amount")] decimal loanAmount,
        [Description("Loan type")] string loanType,
        [Description("Monthly payment calculated for the loan")] decimal monthlyPayment)
    {
        logger.LogDebug("[PLUGIN:Credit] AssessAffordability ← annualIncome={Income}, loanAmount={Loan}, type={Type}, monthlyPayment={Payment}",
            annualIncome, loanAmount, loanType, monthlyPayment);
        var monthlyIncome = annualIncome / 12m;
        var paymentToIncomeRatio = Math.Round(monthlyPayment / monthlyIncome * 100, 2);
        var loanToIncomeRatio = Math.Round(loanAmount / annualIncome, 2);

        // Maximum multipliers by loan type
        var maxLtiRatio = loanType.ToLower() switch
        {
            "mortgage" or "homeequityloan" => 4.5m,
            "businessloan" => 3.0m,
            "autoloan" => 0.5m,
            _ => 2.0m
        };

        var affordable = paymentToIncomeRatio <= 35 && loanToIncomeRatio <= maxLtiRatio;

        var result = JsonSerializer.Serialize(new
        {
            paymentToIncomePercent = paymentToIncomeRatio,
            loanToIncomeRatio,
            maxAllowedLtiRatio = maxLtiRatio,
            affordable,
            assessment = affordable
                ? "Loan amount is within affordable limits"
                : "Loan amount may exceed safe affordability thresholds"
        });
        logger.LogDebug("[PLUGIN:Credit] AssessAffordability → {Result}", result);
        return result;
    }
}
