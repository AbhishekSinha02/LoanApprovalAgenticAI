using System.Text.Json;
using System.Text.RegularExpressions;
using LoanApproval.Agents.Plugins;
using LoanApproval.Shared.Interfaces;
using LoanApproval.Shared.Models;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;

namespace LoanApproval.Agents.Orchestration;

public sealed class LoanApprovalOrchestrator(
    Kernel kernel,
    DocumentAnalysisPlugin documentPlugin,
    CreditAssessmentPlugin creditPlugin,
    RiskAssessmentPlugin riskPlugin,
    CompliancePlugin compliancePlugin,
    ILogger<LoanApprovalOrchestrator> logger) : ILoanOrchestrator
{
    public async Task<LoanDecision> ProcessApplicationAsync(
        LoanApplication application,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Processing loan application {ApplicationId}", application.ApplicationId);

        var ctx = await ComputePluginContextAsync(application, cancellationToken);
        var appPrompt = BuildApplicationPrompt(application, ctx);
        logger.LogInformation("Plugin data pre-computed — starting 5-agent analysis pipeline");

        var settings = new OpenAIPromptExecutionSettings { Temperature = 0.1, MaxTokens = 1024 };
        var agentReports = new List<AgentReport>();

        // Specialist agents each get a fresh chat history — no cross-contamination.
        // With llama-family models, sharing conversation history causes agents to produce
        // empty responses when they perceive their topic is already covered.
        await RunAgentAsync(AgentNames.DocumentAnalyst,   BuildDocumentAnalystPrompt(),   appPrompt, settings, agentReports, cancellationToken);
        await RunAgentAsync(AgentNames.CreditAnalyst,     BuildCreditAnalystPrompt(),     appPrompt, settings, agentReports, cancellationToken);
        await RunAgentAsync(AgentNames.RiskAnalyst,       BuildRiskAnalystPrompt(),       appPrompt, settings, agentReports, cancellationToken);
        await RunAgentAsync(AgentNames.ComplianceOfficer, BuildComplianceOfficerPrompt(), appPrompt, settings, agentReports, cancellationToken);

        // LoanOfficer sees all specialist reports plus the application data
        var officerPrompt = BuildOfficerContext(appPrompt, agentReports);
        await RunAgentAsync(AgentNames.LoanOfficer, BuildLoanOfficerPrompt(), officerPrompt, settings, agentReports, cancellationToken);

        return BuildFinalDecision(application, agentReports);
    }

    private async Task RunAgentAsync(
        string agentName,
        string systemPrompt,
        string userMessage,
        OpenAIPromptExecutionSettings settings,
        List<AgentReport> reports,
        CancellationToken ct)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        logger.LogInformation("── Starting [{Agent}] ──", agentName);

        try
        {
            var history = new ChatHistory();
            history.AddSystemMessage(systemPrompt);
            history.AddUserMessage(userMessage);

            var chatService = kernel.GetRequiredService<IChatCompletionService>();
            var response = await chatService.GetChatMessageContentAsync(history, settings, kernel, ct);
            sw.Stop();

            var content = response.Content ?? string.Empty;
            logger.LogInformation("── [{Agent}] replied in {Elapsed:mm\\:ss} ({Chars} chars) ──",
                agentName, sw.Elapsed, content.Length);
            logger.LogDebug("{Content}", content);

            if (string.IsNullOrWhiteSpace(content))
            {
                logger.LogWarning("  [EMPTY] [{Agent}] returned no content", agentName);
                return;
            }

            var fakeMsg = new ChatMessageContent(AuthorRole.Assistant, content) { AuthorName = agentName };
            if (TryParseAgentReport(fakeMsg, out var report) && report is not null)
            {
                if (report.AgentName != agentName)
                    report = report with { AgentName = agentName };
                reports.Add(report);
                logger.LogInformation("  ✓ passed={Passed}  score={Score}  concerns={Concerns}",
                    report.Passed, report.Score, report.Concerns.Count);
            }
            else
            {
                var preview = content.Length > 600 ? content[..600] + $"... [{content.Length - 600} more]" : content;
                logger.LogWarning("  ✗ Could not parse JSON from [{Agent}]:\n{Preview}", agentName, preview);
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            logger.LogWarning("  [{Agent}] cancelled after {Elapsed:mm\\:ss}", agentName, sw.Elapsed);
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "  [{Agent}] threw an exception", agentName);
        }
    }

    private static string BuildOfficerContext(string appPrompt, IReadOnlyList<AgentReport> reports)
    {
        if (reports.Count == 0)
            return appPrompt;

        var sb = new System.Text.StringBuilder(appPrompt);
        sb.AppendLine().AppendLine().AppendLine("SPECIALIST AGENT REPORTS").AppendLine("========================");
        foreach (var r in reports)
        {
            sb.AppendLine($"--- {r.AgentName} (passed={r.Passed}, score={r.Score}) ---");
            sb.AppendLine($"Summary  : {r.Summary}");
            if (r.Findings.Count > 0)
                sb.AppendLine($"Findings : {string.Join(" | ", r.Findings)}");
            if (r.Concerns.Count > 0)
                sb.AppendLine($"Concerns : {string.Join(" | ", r.Concerns)}");
        }
        return sb.ToString();
    }

    // ── Plugin pre-computation ─────────────────────────────────────────────────

    private async Task<PluginContext> ComputePluginContextAsync(
        LoanApplication app, CancellationToken ct)
    {
        logger.LogInformation("Pre-computing plugin data ({DocCount} document(s))", app.Documents.Count);

        // Document analysis
        var extractions = new List<string>();
        var authenticity = new List<string>();
        foreach (var doc in app.Documents)
        {
            extractions.Add(await documentPlugin.ExtractDocumentDataAsync(
                doc.DocumentId.ToString(), doc.Type.ToString()));
            authenticity.Add(await documentPlugin.VerifyDocumentAuthenticityAsync(
                doc.DocumentId.ToString(), doc.Type.ToString()));
        }

        var crossRef = extractions.Count > 0
            ? documentPlugin.CrossReferenceApplicantData(
                extractions[0], app.Applicant.AnnualIncome, app.Applicant.EmployerName ?? "Unknown")
            : JsonSerializer.Serialize(new
            {
                discrepanciesFound = false,
                discrepancies = Array.Empty<string>(),
                crossReferenceScore = 100,
                note = "No documents submitted — assessment based on stated information only"
            });

        // Credit metrics
        var creditEval    = creditPlugin.EvaluateCreditScore(app.Applicant.CreditScore);
        var monthlyPayStr = creditPlugin.CalculateMonthlyPayment(
            app.LoanDetails.RequestedAmount, 8.0m, app.LoanDetails.TermMonths);
        var monthlyPay    = ParseDecimal(monthlyPayStr, "monthlyPayment");
        var monthlyIncome = app.Applicant.AnnualIncome / 12m;
        var dtiStr        = creditPlugin.CalculateDebtToIncomeRatio(
            monthlyIncome, app.Applicant.MonthlyDebtObligations, monthlyPay);
        var dtiRatio      = ParseDecimal(dtiStr, "dtiRatio");
        var affordStr     = creditPlugin.AssessAffordability(
            app.Applicant.AnnualIncome, app.LoanDetails.RequestedAmount,
            app.LoanDetails.LoanType.ToString(), monthlyPay);

        // Risk metrics
        var ltvStr      = riskPlugin.EvaluateLoanToValue(
            app.LoanDetails.RequestedAmount,
            app.LoanDetails.CollateralValue ?? 0m,
            app.LoanDetails.LoanType.ToString());
        var ltvRatio    = ParseDecimal(ltvStr, "ltvRatio");
        var riskStr     = riskPlugin.CalculateRiskScore(
            app.Applicant.CreditScore, dtiRatio,
            app.Applicant.YearsAtCurrentJob,
            app.LoanDetails.LoanType.ToString(), ltvRatio);
        var riskScore   = ParseInt(riskStr, "riskScore");
        var rateStr     = riskPlugin.DetermineInterestRate(
            riskScore, app.LoanDetails.LoanType.ToString(), app.LoanDetails.TermMonths);

        // Compliance
        var kyc         = await compliancePlugin.RunKycCheckAsync(
            app.Applicant.FullName, app.Applicant.DateOfBirth.ToString("yyyy-MM-dd"),
            app.Applicant.SocialSecurityNumberHash, app.Applicant.Age);
        var aml         = await compliancePlugin.RunAmlScreeningAsync(
            app.Applicant.FullName, app.Applicant.Address.ToString());
        var fairLending = compliancePlugin.CheckFairLendingCompliance(
            "credit score, income, employment history, debt-to-income ratio, collateral, loan-to-value ratio",
            app.LoanDetails.LoanType.ToString());
        var hmda        = compliancePlugin.CheckHmdaRequirements(
            app.LoanDetails.LoanType.ToString(),
            app.LoanDetails.PropertyAddress?.ToString() ?? string.Empty);
        var eligibility = compliancePlugin.ValidateLoanEligibility(
            app.Applicant.Age, app.Applicant.AnnualIncome,
            app.Applicant.CreditScore, app.LoanDetails.RequestedAmount,
            app.LoanDetails.LoanType.ToString());

        logger.LogInformation(
            "Plugin context ready — creditScore={Credit}, dti={DTI:F1}%, riskScore={Risk}, monthlyPay={Pay:F2}",
            app.Applicant.CreditScore, dtiRatio, riskScore, monthlyPay);

        return new PluginContext(
            DocumentExtractions: extractions,
            AuthenticityChecks: authenticity,
            CrossReference: crossRef,
            CreditEvaluation: creditEval,
            MonthlyPayment: monthlyPayStr,
            DebtToIncomeRatio: dtiStr,
            Affordability: affordStr,
            LoanToValue: ltvStr,
            RiskScore: riskStr,
            InterestRate: rateStr,
            KycCheck: kyc,
            AmlScreening: aml,
            FairLendingCompliance: fairLending,
            HmdaRequirements: hmda,
            LoanEligibility: eligibility);
    }

    private sealed record PluginContext(
        IReadOnlyList<string> DocumentExtractions,
        IReadOnlyList<string> AuthenticityChecks,
        string CrossReference,
        string CreditEvaluation,
        string MonthlyPayment,
        string DebtToIncomeRatio,
        string Affordability,
        string LoanToValue,
        string RiskScore,
        string InterestRate,
        string KycCheck,
        string AmlScreening,
        string FairLendingCompliance,
        string HmdaRequirements,
        string LoanEligibility);

    // ── Prompt builders ────────────────────────────────────────────────────────

    private static string BuildApplicationPrompt(LoanApplication app, PluginContext ctx)
    {
        var docSection = app.Documents.Count == 0
            ? "  (no documents submitted)"
            : string.Join("\n", app.Documents.Select((d, i) =>
                $"  [{i + 1}] {d.Type}: {d.FileName ?? d.StorageUri}\n" +
                $"      Extraction : {ctx.DocumentExtractions.ElementAtOrDefault(i) ?? "n/a"}\n" +
                $"      Authenticity: {ctx.AuthenticityChecks.ElementAtOrDefault(i) ?? "n/a"}"));

        return $"""
        LOAN APPLICATION
        ================
        Application ID : {app.ApplicationId}
        Submitted At   : {app.SubmittedAt:u}

        APPLICANT
        ---------
        Name           : {app.Applicant.FullName}
        Date of Birth  : {app.Applicant.DateOfBirth:yyyy-MM-dd}  (Age {app.Applicant.Age})
        Address        : {app.Applicant.Address}
        Employment     : {app.Applicant.EmploymentStatus} — {app.Applicant.YearsAtCurrentJob} yrs at {app.Applicant.EmployerName ?? "N/A"}
        Annual Income  : ${app.Applicant.AnnualIncome:N2}
        Monthly Debt   : ${app.Applicant.MonthlyDebtObligations:N2}
        Credit Score   : {app.Applicant.CreditScore}

        LOAN REQUEST
        ------------
        Type           : {app.LoanDetails.LoanType}
        Amount         : ${app.LoanDetails.RequestedAmount:N2}
        Term           : {app.LoanDetails.TermMonths} months
        Purpose        : {app.LoanDetails.Purpose}
        Collateral     : {(app.LoanDetails.CollateralValue.HasValue ? $"${app.LoanDetails.CollateralValue:N2} — {app.LoanDetails.CollateralDescription}" : "None (unsecured)")}

        PRE-COMPUTED ANALYSIS DATA
        ==========================
        (All values already calculated — agents must use these results, not call any functions.)

        [DOCUMENT ANALYSIS]
        {docSection}
        Cross-reference: {ctx.CrossReference}

        [CREDIT METRICS]
        Credit evaluation : {ctx.CreditEvaluation}
        Monthly payment   : {ctx.MonthlyPayment}
        Debt-to-income    : {ctx.DebtToIncomeRatio}
        Affordability     : {ctx.Affordability}

        [RISK METRICS]
        Loan-to-value     : {ctx.LoanToValue}
        Risk score        : {ctx.RiskScore}
        Interest rate     : {ctx.InterestRate}

        [COMPLIANCE]
        KYC               : {ctx.KycCheck}
        AML               : {ctx.AmlScreening}
        Fair lending      : {ctx.FairLendingCompliance}
        HMDA              : {ctx.HmdaRequirements}
        Eligibility       : {ctx.LoanEligibility}
        """;
    }

    // ── JSON parsing ───────────────────────────────────────────────────────────

    private static readonly JsonSerializerOptions LenientJsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowReadingFromString,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip
    };

    private bool TryParseAgentReport(ChatMessageContent message, out AgentReport? report)
    {
        report = null;
        if (string.IsNullOrWhiteSpace(message.Content)) return false;

        var content = message.Content!;

        // Try ```json ... ``` fence first (common with llama/mistral models)
        var fenceMatch = Regex.Match(content, @"```(?:json)?\s*(\{[\s\S]*?\})\s*```");
        var json = fenceMatch.Success
            ? fenceMatch.Groups[1].Value
            : ExtractBareJson(content);

        if (json is null) return false;

        try
        {
            report = JsonSerializer.Deserialize<AgentReport>(json, LenientJsonOpts);
            return report is not null;
        }
        catch (JsonException ex)
        {
            logger.LogWarning("  JSON parse error ({Msg}) in: {Json}",
                ex.Message, json.Length > 300 ? json[..300] + "..." : json);
            return false;
        }
    }

    private static string? ExtractBareJson(string content)
    {
        var start = content.IndexOf('{');
        var end   = content.LastIndexOf('}');
        return start >= 0 && end > start ? content[start..(end + 1)] : null;
    }

    private static decimal ParseDecimal(string json, string property)
    {
        try
        {
            var el = JsonSerializer.Deserialize<JsonElement>(json);
            return el.TryGetProperty(property, out var v) ? v.GetDecimal() : 0m;
        }
        catch { return 0m; }
    }

    private static int ParseInt(string json, string property)
    {
        try
        {
            var el = JsonSerializer.Deserialize<JsonElement>(json);
            return el.TryGetProperty(property, out var v) ? v.GetInt32() : 0;
        }
        catch { return 0; }
    }

    // ── Decision builder ───────────────────────────────────────────────────────

    private static LoanDecision BuildFinalDecision(LoanApplication application, List<AgentReport> reports)
    {
        var officerReport = reports.FirstOrDefault(r => r.AgentName == AgentNames.LoanOfficer);
        var overallScore  = reports.Any() ? (int)Math.Round(reports.Average(r => r.Score)) : 0;
        var riskReport    = reports.FirstOrDefault(r => r.AgentName == AgentNames.RiskAnalyst);

        return new LoanDecision
        {
            ApplicationId  = application.ApplicationId,
            Status         = ParseDecisionStatus(officerReport),
            Reasoning      = officerReport?.Summary
                             ?? "Your application has been received and is pending manual review. A loan officer will contact you within 2 business days.",
            OverallScore   = officerReport?.Score ?? overallScore,
            RiskLevel      = ParseRiskLevel(riskReport),
            Conditions     = officerReport?.Findings ?? [],
            AgentReports   = reports,
            ApprovedAmount = TryGetMetadata<decimal>(officerReport, "approvedAmount"),
            InterestRate   = TryGetMetadata<decimal>(officerReport, "interestRate"),
            TermMonths     = application.LoanDetails.TermMonths
        };
    }

    private static DecisionStatus ParseDecisionStatus(AgentReport? report)
    {
        if (report is null) return DecisionStatus.PendingManualReview;
        return report.Passed switch
        {
            true when report.Concerns.Count == 0 => DecisionStatus.Approved,
            true => DecisionStatus.ConditionallyApproved,
            _ => DecisionStatus.Rejected
        };
    }

    private static RiskLevel ParseRiskLevel(AgentReport? riskReport)
    {
        if (riskReport is null) return RiskLevel.Medium;
        if (riskReport.Metadata.TryGetValue("riskLevel", out var rl))
        {
            return rl?.ToString() switch
            {
                "Low"      => RiskLevel.Low,
                "Medium"   => RiskLevel.Medium,
                "High"     => RiskLevel.High,
                "VeryHigh" => RiskLevel.VeryHigh,
                _ => RiskLevel.Medium
            };
        }
        return riskReport.Score < 25 ? RiskLevel.Low
             : riskReport.Score < 50 ? RiskLevel.Medium
             : riskReport.Score < 70 ? RiskLevel.High
             : RiskLevel.VeryHigh;
    }

    private static T? TryGetMetadata<T>(AgentReport? report, string key)
    {
        if (report?.Metadata.TryGetValue(key, out var val) is not true || val is null)
            return default;
        try
        {
            // System.Text.Json deserializes nested objects as JsonElement, not boxed primitives.
            if (val is JsonElement el)
                return el.Deserialize<T>();
            return (T)Convert.ChangeType(val, typeof(T));
        }
        catch { return default; }
    }

    // ── Agent system prompts (reference pre-computed data, no function calls) ──

    private static string BuildDocumentAnalystPrompt() => """
        You are a Document Analysis Specialist. Your ONLY job is to assess the [DOCUMENT ANALYSIS]
        section — document authenticity, extractions, and the cross-reference check.
        Do NOT comment on credit scores, risk scores, compliance, or interest rates.

        Report findings ONLY from [DOCUMENT ANALYSIS]. Other agents will cover credit/risk/compliance.

        OUTPUT RULE: Your entire response must be exactly one JSON object. No preamble, no notes,
        no explanation, no markdown — just the raw JSON starting with { and ending with }.

        {"agentName":"DocumentAnalyst","summary":"<document-specific findings only>","passed":true,"score":<0-100>,"findings":["<document finding>"],"concerns":["<document concern>"],"metadata":{}}

        Score guide: 90-100=all clear, 70-89=minor issues, 50-69=missing docs, <50=major issues.
        passed=false only if there are major authenticity failures or critical discrepancies.
        """;

    private static string BuildCreditAnalystPrompt() => """
        You are a Credit Assessment Specialist. Your ONLY job is to assess the [CREDIT METRICS]
        section and report your independent credit opinion.

        Always output your JSON report — do not skip this step or defer to other agents.

        Your job:
        1. Review the credit evaluation (score category, risk premium).
        2. Review the monthly payment and debt-to-income ratio.
        3. Review the affordability assessment.
        4. Form your own independent credit opinion.

        OUTPUT RULE: Your entire response must be exactly one JSON object. No preamble, no notes,
        no explanation, no markdown — just the raw JSON starting with { and ending with }.

        {"agentName":"CreditAnalyst","summary":"<your credit assessment>","passed":true,"score":<0-100>,"findings":["<finding>"],"concerns":["<concern>"],"metadata":{"creditScoreCategory":"<category>","dtiRatio":<number>,"monthlyPayment":<number>,"affordable":true}}

        passed=true when credit score ≥580 AND DTI <43% AND affordable=true.
        """;

    private static string BuildRiskAnalystPrompt() => """
        You are a Risk Assessment Specialist. All risk metrics have already been calculated
        and are provided in the [RISK METRICS] section of the user message.

        Your job:
        1. Review the loan-to-value ratio.
        2. Review the composite risk score and its level (Low/Medium/High/VeryHigh).
        3. Review the suggested interest rate.
        4. Form an overall risk opinion.

        OUTPUT RULE: Your entire response must be exactly one JSON object. No preamble, no notes,
        no explanation, no markdown — just the raw JSON starting with { and ending with }.

        {"agentName":"RiskAnalyst","summary":"<findings>","passed":true,"score":<risk score 0-100>,"findings":["<finding>"],"concerns":["<concern>"],"metadata":{"riskLevel":"<Low|Medium|High|VeryHigh>","suggestedRate":<number>,"ltvRatio":<number>}}

        passed=true when risk score <70 (not High or VeryHigh).
        """;

    private static string BuildComplianceOfficerPrompt() => """
        You are a Compliance Officer. Your ONLY job is to assess the [COMPLIANCE] section
        and report your independent compliance opinion.

        Always output your JSON report — do not skip this step or defer to other agents.

        Your job:
        1. Review KYC and AML results.
        2. Review fair lending (ECOA) compliance.
        3. Review HMDA requirements.
        4. Review loan eligibility.
        5. Identify any regulatory violations or gaps.

        OUTPUT RULE: Your entire response must be exactly one JSON object. No preamble, no notes,
        no explanation, no markdown — just the raw JSON starting with { and ending with }.

        {"agentName":"ComplianceOfficer","summary":"<your compliance assessment>","passed":true,"score":<0-100>,"findings":["<check passed>"],"concerns":["<violation>"],"metadata":{"kycPass":true,"amlPass":true,"ecoaCompliant":true,"hmdaCompliant":true,"eligibilityPass":true}}

        passed=true only when KYC, AML, ECOA, and eligibility all pass. Score=100 fully compliant; deduct 25 per failed check.
        """;

    private static string BuildLoanOfficerPrompt() => """
        You are the Senior Loan Officer making the final underwriting decision.
        Review ALL prior agent reports in the conversation, then use the PRE-COMPUTED ANALYSIS DATA.

        Your job:
        1. Weigh the four agent reports (document 20%, credit 35%, risk 30%, compliance 15%).
        2. Apply hard-denial rules: reject if KYC fails, compliance fails, credit <500, or DTI >50%.
        3. Score ≥75 + Low/Medium risk = Approved. Score ≥60 = ConditionallyApproved. Otherwise Rejected.
        4. State the approved amount and interest rate from the risk data.

        OUTPUT RULE: Your entire response must be exactly one JSON object. No preamble, no notes,
        no explanation, no markdown — just the raw JSON starting with { and ending with }.

        {"agentName":"LoanOfficer","summary":"<clear decision explanation for applicant>","passed":true,"score":<0-100>,"findings":["<condition>"],"concerns":["<reason>"],"metadata":{"decisionStatus":"<Approved|ConditionallyApproved|Rejected|PendingManualReview>","approvedAmount":<number or null>,"interestRate":<number or null>,"riskLevel":"<Low|Medium|High|VeryHigh>"}}

        passed=true only for Approved or ConditionallyApproved.
        """;
}
