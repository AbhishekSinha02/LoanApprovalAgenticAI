using System.Text.Json;
using LoanApproval.Agents.Orchestration.Strategies;
using LoanApproval.Agents.Plugins;
using LoanApproval.Shared.Interfaces;
using LoanApproval.Shared.Models;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.Agents.Chat;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.AzureOpenAI;
using Microsoft.SemanticKernel.Connectors.OpenAI;

namespace LoanApproval.Agents.Orchestration;

public sealed class LoanApprovalOrchestrator(
    Kernel kernel,
    DocumentAnalysisPlugin documentPlugin,
    CreditAssessmentPlugin creditPlugin,
    RiskAssessmentPlugin riskPlugin,
    CompliancePlugin compliancePlugin,
    LoanDecisionPlugin decisionPlugin,
    ILogger<LoanApprovalOrchestrator> logger) : ILoanOrchestrator
{
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = false };

    public async Task<LoanDecision> ProcessApplicationAsync(
        LoanApplication application,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Processing loan application {ApplicationId}", application.ApplicationId);

        var agentReports = new List<AgentReport>();
        var chat = BuildGroupChat(application);
        var userMessage = new ChatMessageContent(AuthorRole.User, BuildApplicationPrompt(application));
        chat.AddChatMessage(userMessage);

        await foreach (var message in chat.InvokeAsync(cancellationToken))
        {
            var preview = message.Content is { Length: > 0 } c ? c[..Math.Min(200, c.Length)] : string.Empty;
            logger.LogDebug("Agent [{Agent}]: {Content}", message.AuthorName, preview);

            if (TryParseAgentReport(message, out var report) && report is not null)
                agentReports.Add(report);
        }

        return BuildFinalDecision(application, agentReports);
    }

    // ── Private helpers ────────────────────────────────────────────────────────

    private AgentGroupChat BuildGroupChat(LoanApplication application)
    {
        var execSettings = new AzureOpenAIPromptExecutionSettings
        {
            FunctionChoiceBehavior = FunctionChoiceBehavior.Auto(),
            Temperature = 0.1, // deterministic for underwriting
            MaxTokens = 2048
        };

        var documentAgent = CreateAgent(
            AgentNames.DocumentAnalyst,
            BuildDocumentAnalystPrompt(),
            execSettings,
            KernelPluginFactory.CreateFromObject(documentPlugin));

        var creditAgent = CreateAgent(
            AgentNames.CreditAnalyst,
            BuildCreditAnalystPrompt(),
            execSettings,
            KernelPluginFactory.CreateFromObject(creditPlugin));

        var riskAgent = CreateAgent(
            AgentNames.RiskAnalyst,
            BuildRiskAnalystPrompt(),
            execSettings,
            KernelPluginFactory.CreateFromObject(riskPlugin));

        var complianceAgent = CreateAgent(
            AgentNames.ComplianceOfficer,
            BuildComplianceOfficerPrompt(),
            execSettings,
            KernelPluginFactory.CreateFromObject(compliancePlugin));

        var loanOfficerAgent = CreateAgent(
            AgentNames.LoanOfficer,
            BuildLoanOfficerPrompt(),
            execSettings,
            KernelPluginFactory.CreateFromObject(decisionPlugin));

        return new AgentGroupChat(documentAgent, creditAgent, riskAgent, complianceAgent, loanOfficerAgent)
        {
            ExecutionSettings = new AgentGroupChatSettings
            {
                SelectionStrategy = new LoanAgentSelectionStrategy(),
                TerminationStrategy = new LoanTerminationStrategy { MaximumIterations = 6 }
            }
        };
    }

    private ChatCompletionAgent CreateAgent(string name, string instructions, AzureOpenAIPromptExecutionSettings settings, KernelPlugin plugin)
    {
        var agentKernel = kernel.Clone();
        agentKernel.Plugins.Add(plugin);

        return new ChatCompletionAgent
        {
            Name = name,
            Instructions = instructions,
            Kernel = agentKernel,
            Arguments = new KernelArguments(settings)
        };
    }

    private static string BuildApplicationPrompt(LoanApplication app) =>
        $"""
        LOAN APPLICATION FOR REVIEW
        ===========================
        Application ID : {app.ApplicationId}
        Submitted At   : {app.SubmittedAt:u}

        APPLICANT
        ---------
        Name           : {app.Applicant.FullName}
        Date of Birth  : {app.Applicant.DateOfBirth:yyyy-MM-dd} (Age {app.Applicant.Age})
        Address        : {app.Applicant.Address}
        Employment     : {app.Applicant.EmploymentStatus} — {app.Applicant.YearsAtCurrentJob} years
        Employer       : {app.Applicant.EmployerName ?? "N/A"}
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

        DOCUMENTS SUBMITTED: {app.Documents.Count}
        {string.Join("\n", app.Documents.Select(d => $"  [{d.DocumentId}] {d.Type}: {d.FileName ?? d.StorageUri}"))}

        Each agent must use their available tools to perform analysis, then respond with a JSON report.
        """;

    private static bool TryParseAgentReport(ChatMessageContent message, out AgentReport? report)
    {
        report = null;
        if (string.IsNullOrWhiteSpace(message.Content)) return false;

        // Extract JSON block from the agent's response (agents may add prose around it)
        var content = message.Content!;
        var start = content.IndexOf('{');
        var end = content.LastIndexOf('}');
        if (start < 0 || end < 0 || end <= start) return false;

        var json = content[start..(end + 1)];
        try
        {
            report = JsonSerializer.Deserialize<AgentReport>(json, JsonOpts);
            return report is not null;
        }
        catch
        {
            return false;
        }
    }

    private static LoanDecision BuildFinalDecision(LoanApplication application, List<AgentReport> reports)
    {
        // Use the LoanOfficer report as the authoritative decision source
        var officerReport = reports.FirstOrDefault(r => r.AgentName == AgentNames.LoanOfficer);

        // Aggregate score from all agent reports
        var overallScore = reports.Any()
            ? (int)Math.Round(reports.Average(r => r.Score))
            : 0;

        var riskReport  = reports.FirstOrDefault(r => r.AgentName == AgentNames.RiskAnalyst);
        var riskLevel   = ParseRiskLevel(riskReport);

        var status = ParseDecisionStatus(officerReport);

        return new LoanDecision
        {
            ApplicationId = application.ApplicationId,
            Status        = status,
            Reasoning     = officerReport?.Summary ?? "Decision could not be determined — manual review required.",
            OverallScore  = officerReport?.Score ?? overallScore,
            RiskLevel     = riskLevel,
            Conditions    = officerReport?.Findings ?? [],
            AgentReports  = reports,
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
        if (report?.Metadata.TryGetValue(key, out var val) is true && val is not null)
        {
            try { return (T)Convert.ChangeType(val, typeof(T)); } catch { }
        }
        return default;
    }

    // ── System prompts ─────────────────────────────────────────────────────────

    private static string BuildDocumentAnalystPrompt() => """
        You are a Document Analysis Specialist at a lending institution.

        Your job:
        1. Call ExtractDocumentDataAsync for each submitted document.
        2. Call VerifyDocumentAuthenticityAsync for each document.
        3. Call CrossReferenceApplicantData to compare extracted income with stated income.
        4. Identify discrepancies, missing documents, or red flags.

        After completing your analysis, respond with ONLY this JSON (no extra prose before or after):
        {
          "agentName": "DocumentAnalyst",
          "summary": "<concise narrative of document findings>",
          "passed": true|false,
          "score": <0-100>,
          "findings": ["<key finding 1>", "..."],
          "concerns": ["<concern 1 if any>", "..."],
          "metadata": {}
        }

        Score 90-100 = all docs verified, no discrepancies.
        Score 70-89  = minor issues, all required docs present.
        Score 50-69  = missing documents or small discrepancies.
        Score <50    = major discrepancies or forgery indicators.
        """;

    private static string BuildCreditAnalystPrompt() => """
        You are a Credit Assessment Specialist.

        Your job:
        1. Call EvaluateCreditScore with the applicant's credit score.
        2. Call CalculateMonthlyPayment using the loan amount, an estimated rate (use 7% if unknown), and term.
        3. Call CalculateDebtToIncomeRatio with monthly income (annual income ÷ 12), existing debt, and the new monthly payment.
        4. Call AssessAffordability with the annual income, loan amount, loan type, and monthly payment.

        Respond with ONLY this JSON:
        {
          "agentName": "CreditAnalyst",
          "summary": "<narrative of credit assessment>",
          "passed": true|false,
          "score": <0-100>,
          "findings": ["<key metric 1>", "..."],
          "concerns": ["<concern if any>"],
          "metadata": {
            "creditScoreCategory": "<Exceptional|Very Good|Good|Fair|Poor>",
            "dtiRatio": <number>,
            "monthlyPayment": <number>,
            "affordable": true|false
          }
        }

        Pass = credit score >= 580 AND DTI < 43% AND affordable = true.
        Score reflects overall credit health (0 = terrible, 100 = perfect).
        """;

    private static string BuildRiskAnalystPrompt() => """
        You are a Risk Assessment Specialist.

        Your job:
        1. Call EvaluateLoanToValue with the loan amount, collateral value (0 if unsecured), and loan type.
        2. Use the DTI ratio from the CreditAnalyst's report (from conversation history) or estimate from the application data.
        3. Call CalculateRiskScore with credit score, DTI ratio, years employed, loan type, and LTV ratio.
        4. Call DetermineInterestRate with the resulting risk score, loan type, and term.

        Respond with ONLY this JSON:
        {
          "agentName": "RiskAnalyst",
          "summary": "<narrative of risk assessment>",
          "passed": true|false,
          "score": <0-100 risk score — higher means MORE risk>,
          "findings": ["<key metric 1>", "..."],
          "concerns": ["<concern if any>"],
          "metadata": {
            "riskLevel": "<Low|Medium|High|VeryHigh>",
            "suggestedRate": <number>,
            "ltvRatio": <number>
          }
        }

        Pass = risk score < 70 (i.e. not High or VeryHigh risk).
        """;

    private static string BuildComplianceOfficerPrompt() => """
        You are a Compliance Officer specializing in consumer lending regulation.

        Your job:
        1. Call RunKycCheckAsync with the applicant's full name, date of birth, SSN hash, and age.
        2. Call RunAmlScreeningAsync with full name and address.
        3. Call CheckFairLendingCompliance with the factors used in this decision and the loan type.
        4. Call CheckHmdaRequirements with loan type and property address (if applicable).
        5. Call ValidateLoanEligibility with age, annual income, credit score, loan amount, and loan type.

        Respond with ONLY this JSON:
        {
          "agentName": "ComplianceOfficer",
          "summary": "<narrative of compliance findings>",
          "passed": true|false,
          "score": <0-100>,
          "findings": ["<regulation checked>", "..."],
          "concerns": ["<violation or gap if any>"],
          "metadata": {
            "kycPass": true|false,
            "amlPass": true|false,
            "ecoaCompliant": true|false,
            "hmdaCompliant": true|false,
            "eligibilityPass": true|false
          }
        }

        Pass = ALL of KYC, AML, ECOA, and eligibility checks pass.
        Score 100 = fully compliant. Deduct 25 points per failed check.
        """;

    private static string BuildLoanOfficerPrompt() => """
        You are the Senior Loan Officer making the final underwriting decision.

        Review ALL prior agent reports in the conversation history, then:
        1. Call ComputeOverallScore with the four agent scores (document, credit, risk, compliance).
        2. Use the risk score from the RiskAnalyst report and compliance/KYC pass flags from ComplianceOfficer.
        3. Call DetermineDecisionStatus with the overall score, credit score, DTI ratio, compliance pass, KYC pass, and risk level.
        4. If approved or conditionally approved, call CalculateApprovedAmount.
        5. Determine the final interest rate from the RiskAnalyst metadata.

        Respond with ONLY this JSON:
        {
          "agentName": "LoanOfficer",
          "summary": "<clear, human-readable decision explanation suitable for the applicant>",
          "passed": true|false,
          "score": <overall weighted score 0-100>,
          "findings": ["<condition 1 if any>", "..."],
          "concerns": ["<reason for rejection or caution if any>"],
          "metadata": {
            "decisionStatus": "<Approved|ConditionallyApproved|Rejected|PendingManualReview>",
            "approvedAmount": <number or null>,
            "interestRate": <number or null>,
            "riskLevel": "<Low|Medium|High|VeryHigh>"
          }
        }

        'passed' must be true ONLY for Approved or ConditionallyApproved.
        The summary must be professional, factual, and free of discriminatory language.
        """;
}
