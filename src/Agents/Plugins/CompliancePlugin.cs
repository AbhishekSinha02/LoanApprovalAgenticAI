using System.ComponentModel;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;

namespace LoanApproval.Agents.Plugins;

/// <summary>
/// Rule-based compliance checks: KYC, AML, ECOA fair lending, and HMDA reporting flags.
/// Production integrations would call Experian/Equifax KYC APIs and OFAC watchlist services.
/// </summary>
public sealed class CompliancePlugin(ILogger<CompliancePlugin> logger)
{
    [KernelFunction, Description("Run KYC (Know Your Customer) identity verification.")]
    public async Task<string> RunKycCheckAsync(
        [Description("Applicant full name")] string fullName,
        [Description("Date of birth (yyyy-MM-dd)")] string dateOfBirth,
        [Description("SHA-256 hash of SSN (never the raw SSN)")] string ssnHash,
        [Description("Applicant age in years")] int applicantAge)
    {
        logger.LogInformation("Running KYC for applicant {Name}", fullName);
        await Task.Delay(40); // simulate external call

        var issues = new List<string>();

        if (applicantAge < 18)
            issues.Add("Applicant is under 18 — not eligible for credit.");

        if (applicantAge > 110)
            issues.Add("Applicant age appears invalid.");

        if (string.IsNullOrWhiteSpace(fullName) || fullName.Split(' ').Length < 2)
            issues.Add("Full name could not be verified (missing surname).");

        return JsonSerializer.Serialize(new
        {
            kycPass = issues.Count == 0,
            identityVerified = issues.Count == 0,
            issues,
            verifiedAt = DateTime.UtcNow
        });
    }

    [KernelFunction, Description("Run AML (Anti-Money Laundering) OFAC/sanctions watchlist screening.")]
    public async Task<string> RunAmlScreeningAsync(
        [Description("Applicant full name")] string fullName,
        [Description("Applicant residential address")] string applicantAddress)
    {
        logger.LogInformation("Running AML screening for {Name}", fullName);
        await Task.Delay(60); // simulate OFAC API call

        // Production: call Treasury OFAC SDN list / Dow Jones Watchlist / LexisNexis
        return JsonSerializer.Serialize(new
        {
            amlPass = true,
            watchlistMatch = false,
            sanctionsMatch = false,
            pepMatch = false,
            screeningProvider = "OFAC-SDN",
            screenedAt = DateTime.UtcNow
        });
    }

    [KernelFunction, Description("Check fair lending compliance per ECOA and Fair Housing Act — ensures decision factors are not discriminatory.")]
    public string CheckFairLendingCompliance(
        [Description("List of factors used in the decision, comma-separated")] string decisionFactors,
        [Description("Loan type")] string loanType)
    {
        // Protected characteristics that must NEVER appear in decision logic
        var prohibitedFactors = new[]
        {
            "race", "color", "religion", "national origin", "sex", "gender",
            "marital status", "age", "disability", "familial status", "source of income"
        };

        var lowerFactors = decisionFactors.ToLower();
        var violations = prohibitedFactors.Where(f => lowerFactors.Contains(f)).ToList();

        return JsonSerializer.Serialize(new
        {
            ecoaCompliant = violations.Count == 0,
            violations,
            allowedFactors = new[]
            {
                "credit score", "debt-to-income ratio", "income", "employment history",
                "loan amount", "collateral", "payment history", "loan-to-value ratio"
            },
            checkedAt = DateTime.UtcNow
        });
    }

    [KernelFunction, Description("Verify HMDA (Home Mortgage Disclosure Act) reporting requirements for applicable loans.")]
    public string CheckHmdaRequirements(
        [Description("Loan type")] string loanType,
        [Description("Property address if mortgage or home equity loan; empty otherwise")] string propertyAddress)
    {
        var hmdaApplicable = loanType.ToLower() is "mortgage" or "homeequityloan";

        if (!hmdaApplicable)
            return JsonSerializer.Serialize(new { hmdaRequired = false, loanType });

        var missingFields = new List<string>();
        if (string.IsNullOrWhiteSpace(propertyAddress))
            missingFields.Add("Property address is required for HMDA reporting.");

        return JsonSerializer.Serialize(new
        {
            hmdaRequired = true,
            hmdaCompliant = missingFields.Count == 0,
            missingFields,
            reportingCategory = "Covered Loan",
            checkedAt = DateTime.UtcNow
        });
    }

    [KernelFunction, Description("Validate basic loan eligibility rules (age, income floor, minimum credit score policy).")]
    public string ValidateLoanEligibility(
        [Description("Applicant age in years")] int applicantAge,
        [Description("Annual gross income")] decimal annualIncome,
        [Description("FICO credit score")] int creditScore,
        [Description("Requested loan amount")] decimal loanAmount,
        [Description("Loan type")] string loanType)
    {
        var violations = new List<string>();

        if (applicantAge < 18)
            violations.Add("Applicant must be at least 18 years old.");

        if (annualIncome < 12000)
            violations.Add("Annual income below the $12,000 minimum threshold.");

        if (creditScore < 500)
            violations.Add($"Credit score {creditScore} is below the 500-point minimum policy floor.");

        // Minimum income-to-loan guardrail
        if (loanAmount > annualIncome * 10)
            violations.Add("Requested loan amount exceeds 10× annual income — policy limit breached.");

        return JsonSerializer.Serialize(new
        {
            eligible = violations.Count == 0,
            violations,
            checkedAt = DateTime.UtcNow
        });
    }
}
