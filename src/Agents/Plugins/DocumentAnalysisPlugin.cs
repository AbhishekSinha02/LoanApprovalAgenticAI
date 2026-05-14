using System.ComponentModel;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;

namespace LoanApproval.Agents.Plugins;

/// <summary>
/// Wraps Azure Document Intelligence (Form Recognizer) for loan document extraction.
/// In local dev (USE_LOCAL_INFRA=true), returns realistic mock data so the agent can
/// still reason about document findings without a live endpoint.
/// </summary>
public sealed class DocumentAnalysisPlugin(ILogger<DocumentAnalysisPlugin> logger)
{
    [KernelFunction, Description("Extract structured data from a loan document using Azure Document Intelligence.")]
    public async Task<string> ExtractDocumentDataAsync(
        [Description("Unique document identifier")] string documentId,
        [Description("Document type: PayStub, BankStatement, TaxReturn, Passport, DriversLicense, PropertyDeed, VehicleTitle, BusinessLicense")] string documentType)
    {
        logger.LogInformation("Extracting data from document {DocumentId} of type {DocumentType}", documentId, documentType);

        // In production this calls Azure Document Intelligence prebuilt-layout / prebuilt-tax.us.w2 etc.
        // For the POC we simulate structured extraction results.
        await Task.Delay(50); // simulate network call

        var extracted = documentType.ToLower() switch
        {
            "paystub" => new Dictionary<string, object>
            {
                ["employerName"] = "Acme Corporation",
                ["employeeId"] = "EMP-12345",
                ["payPeriodEnd"] = DateTime.UtcNow.AddDays(-7).ToString("yyyy-MM-dd"),
                ["grossPay"] = 7500.00m,
                ["netPay"] = 5800.00m,
                ["ytdGross"] = 97500.00m,
                ["federalTax"] = 1250.00m,
                ["stateTax"] = 375.00m
            },
            "bankstatement" => new Dictionary<string, object>
            {
                ["bankName"] = "First National Bank",
                ["accountNumber"] = "****4521",
                ["statementMonth"] = DateTime.UtcNow.AddMonths(-1).ToString("yyyy-MM"),
                ["openingBalance"] = 12400.00m,
                ["closingBalance"] = 14850.00m,
                ["totalDeposits"] = 8500.00m,
                ["totalWithdrawals"] = 6050.00m,
                ["averageBalance"] = 13200.00m,
                ["nsfCount"] = 0
            },
            "taxreturn" => new Dictionary<string, object>
            {
                ["taxYear"] = DateTime.UtcNow.Year - 1,
                ["filingStatus"] = "Single",
                ["adjustedGrossIncome"] = 90000.00m,
                ["taxableIncome"] = 76500.00m,
                ["federalTaxPaid"] = 12800.00m,
                ["selfEmploymentIncome"] = 0m
            },
            "passport" or "driverslicense" => new Dictionary<string, object>
            {
                ["documentNumber"] = $"ID-{Guid.NewGuid():N}".Substring(0, 12).ToUpper(),
                ["issuingAuthority"] = documentType.ToLower() == "passport" ? "US Department of State" : "State DMV",
                ["issueDate"] = DateTime.UtcNow.AddYears(-3).ToString("yyyy-MM-dd"),
                ["expiryDate"] = DateTime.UtcNow.AddYears(7).ToString("yyyy-MM-dd"),
                ["nameOnDocument"] = "John A. Doe",
                ["dateOfBirth"] = "1985-06-15"
            },
            _ => new Dictionary<string, object>
            {
                ["documentType"] = documentType,
                ["extractionStatus"] = "generic",
                ["pages"] = 2,
                ["textConfidence"] = 0.92
            }
        };

        return JsonSerializer.Serialize(new { documentId, documentType, extracted, extractedAt = DateTime.UtcNow });
    }

    [KernelFunction, Description("Verify a document's authenticity — checks for tampering indicators.")]
    public async Task<string> VerifyDocumentAuthenticityAsync(
        [Description("Unique document identifier")] string documentId,
        [Description("Document type")] string documentType)
    {
        logger.LogInformation("Verifying authenticity of document {DocumentId}", documentId);
        await Task.Delay(30);

        // Simulate an authenticity check (production: AI-based forgery detection)
        return JsonSerializer.Serialize(new
        {
            documentId,
            authentic = true,
            confidence = 0.97,
            tamperingIndicators = Array.Empty<string>(),
            metadataConsistent = true,
            verifiedAt = DateTime.UtcNow
        });
    }

    [KernelFunction, Description("Cross-reference extracted document data against applicant-provided information to find discrepancies.")]
    public string CrossReferenceApplicantData(
        [Description("Extracted document data as JSON string")] string extractedDataJson,
        [Description("Applicant-provided income (annual)")] decimal applicantStatedAnnualIncome,
        [Description("Applicant-stated employer name")] string applicantEmployerName)
    {
        // In production this compares fields extracted from docs against what the applicant stated
        // Here we compute a simple plausibility check
        decimal? extractedMonthlyGross = null;

        try
        {
            using var doc = JsonDocument.Parse(extractedDataJson);
            if (doc.RootElement.TryGetProperty("extracted", out var extracted)
                && extracted.TryGetProperty("grossPay", out var gp))
            {
                extractedMonthlyGross = gp.GetDecimal();
            }
        }
        catch { /* malformed JSON — skip numeric comparison */ }

        var discrepancies = new List<string>();

        if (extractedMonthlyGross.HasValue)
        {
            var annualizedExtracted = extractedMonthlyGross.Value * 12;
            var variance = Math.Abs(annualizedExtracted - applicantStatedAnnualIncome) / applicantStatedAnnualIncome;
            if (variance > 0.15m) // >15% difference is a red flag
                discrepancies.Add($"Income discrepancy: stated ${applicantStatedAnnualIncome:N0}/yr vs doc-extracted ${annualizedExtracted:N0}/yr ({variance:P0} variance)");
        }

        return JsonSerializer.Serialize(new
        {
            discrepanciesFound = discrepancies.Count > 0,
            discrepancies,
            crossReferenceScore = discrepancies.Count == 0 ? 100 : Math.Max(0, 100 - discrepancies.Count * 25),
            verifiedAt = DateTime.UtcNow
        });
    }
}
