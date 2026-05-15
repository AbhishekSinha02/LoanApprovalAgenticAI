# Manual Testing Guide — Loan Approval Agentic AI

---

## Prerequisites

### 1. Ollama (local AI)
```powershell
ollama pull llama3.1   # one-time download (~4 GB)
ollama serve           # must be running on http://localhost:11434
```
Verify: `curl http://localhost:11434` → should return a short text response.

### 2. Start the API
```powershell
dotnet run --project src/Api
```
- Swagger UI: http://localhost:5091/swagger
- Health check: http://localhost:5091/health → `{"status":"healthy",...}`

`appsettings.Development.json` already sets `USE_LOCAL_INFRA=true`, so no Azure credentials are needed.

---

## How to See Debug Logs

The API prints structured logs to the console. Log levels are configured in `appsettings.Development.json`:

| Namespace | Level | What you see |
|---|---|---|
| `LoanApproval` | Debug | Every plugin call (input + output), every agent step |
| `Microsoft.SemanticKernel` | Debug | SK function-invoke/result trace |
| `Microsoft.AspNetCore` | Information | HTTP request/response |

### What the console looks like during a request

```
16:05:01 info  LoanApplication: Received loan application abc123 for $25,000 PersonalLoan
16:05:01 info  LoanApprovalOrchestrator: Processing loan application abc123

── AGENT STEP 1 [DocumentAnalyst] ──────────────────────────────────
16:05:02 dbug  [PLUGIN:Document] ExtractDocumentDataAsync ← documentId=00000000-..., type=PayStub
16:05:02 dbug  [PLUGIN:Document] ExtractDocumentDataAsync → {"documentId":"...","extracted":{"grossPay":7500,...}}
16:05:02 dbug  [PLUGIN:Document] CrossReferenceApplicantData ← statedIncome=95000, employer=Acme
16:05:02 dbug  [PLUGIN:Document] CrossReferenceApplicantData → {"discrepanciesFound":false,...}
16:05:05 info  ── AGENT STEP 1 [DocumentAnalyst] ──  ✓ Report parsed — passed=True, score=88

── AGENT STEP 2 [CreditAnalyst] ──────────────────────────────────
16:05:05 dbug  [PLUGIN:Credit] EvaluateCreditScore ← creditScore=760
16:05:05 dbug  [PLUGIN:Credit] EvaluateCreditScore → {"category":"Very Good","meetsMinimum":true,...}
16:05:05 dbug  [PLUGIN:Credit] CalculateMonthlyPayment ← principal=25000, rate=8%, term=60mo
16:05:05 dbug  [PLUGIN:Credit] CalculateMonthlyPayment → {"monthlyPayment":507.91,"totalInterest":5474.6}
16:05:05 dbug  [PLUGIN:Credit] CalculateDebtToIncomeRatio ← monthlyIncome=7916.67, existingDebt=450, newPayment=507.91
16:05:05 dbug  [PLUGIN:Credit] CalculateDebtToIncomeRatio → {"dtiRatio":12.12,"assessment":"Excellent",...}
16:05:08 info  ── AGENT STEP 2 [CreditAnalyst] ──  ✓ Report parsed — passed=True, score=82

── AGENT STEP 3 [RiskAnalyst] ──────────────────────────────────
16:05:08 dbug  [PLUGIN:Risk] EvaluateLoanToValue ← loanAmount=25000, collateral=0, type=PersonalLoan
16:05:08 dbug  [PLUGIN:Risk] EvaluateLoanToValue → {"ltvRatio":0,"assessment":"Unsecured loan — no collateral"}
16:05:08 dbug  [PLUGIN:Risk] CalculateRiskScore ← creditScore=760, dti=12.12%, yearsEmployed=7, type=PersonalLoan, ltv=0%
16:05:08 dbug  [PLUGIN:Risk] CalculateRiskScore → {"riskScore":14,"riskLevel":"Low",...}
16:05:12 info  ── AGENT STEP 3 [RiskAnalyst] ──  ✓ Report parsed — passed=True, score=86

── AGENT STEP 4 [ComplianceOfficer] ──────────────────────────────────
16:05:12 dbug  [PLUGIN:Compliance] RunKycCheckAsync ← name=Sarah Johnson, dob=1985-06-15, age=40
16:05:12 dbug  [PLUGIN:Compliance] RunKycCheckAsync → {"kycPass":true,"identityVerified":true,...}
16:05:12 dbug  [PLUGIN:Compliance] RunAmlScreeningAsync ← name=Sarah Johnson, address=742 Evergreen...
16:05:12 dbug  [PLUGIN:Compliance] RunAmlScreeningAsync → {"amlPass":true,"watchlistMatch":false,...}
16:05:16 info  ── AGENT STEP 4 [ComplianceOfficer] ──  ✓ Report parsed — passed=True, score=100

── AGENT STEP 5 [LoanOfficer] ──────────────────────────────────
16:05:16 dbug  [PLUGIN:Decision] ComputeOverallScore ← doc=88, credit=82, risk=86, compliance=100
16:05:16 dbug  [PLUGIN:Decision] ComputeOverallScore → {"overallScore":87,...}
16:05:16 info  [PLUGIN:Decision] DetermineDecisionStatus → status=Approved, score=87
16:05:20 info  ── AGENT STEP 5 [LoanOfficer] ──  ✓ Report parsed — passed=True, score=87

16:05:20 info  LoanApplication: Application abc123 decided: Approved (score: 87)
```

### Capture logs to a file
```powershell
dotnet run --project src/Api *>&1 | Tee-Object api.log
```
Then search: `Select-String "\[PLUGIN" api.log`

---

## Actual API Response Structure

The `POST /api/loans/apply` endpoint returns a `LoanDecision` object — **not** the original request. It looks like this:

```json
{
  "applicationId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "status": "Approved",
  "reasoning": "Sarah Johnson presents a strong credit profile. Credit score of 760 falls in the Very Good range. Debt-to-income ratio of 12.1% is well within acceptable limits. Employment history of 7 years demonstrates stability. All compliance checks passed. The $25,000 personal loan is approved at 8.0% APR.",
  "approvedAmount": 25000.00,
  "interestRate": 8.0,
  "termMonths": 60,
  "overallScore": 87,
  "riskLevel": "Low",
  "decidedAt": "2026-05-14T16:05:20Z",
  "conditions": [],
  "agentReports": [
    {
      "agentName": "DocumentAnalyst",
      "summary": "No documents submitted. Assessment based on stated information only...",
      "passed": true,
      "score": 88,
      "findings": ["No documents submitted — assessment based on stated information"],
      "concerns": [],
      "metadata": {},
      "completedAt": "2026-05-14T16:05:05Z"
    },
    {
      "agentName": "CreditAnalyst",
      "summary": "Credit score 760 (Very Good). Monthly payment of $507.91 gives a DTI of 12.1%...",
      "passed": true,
      "score": 82,
      "findings": ["Credit score: 760 — Very Good", "DTI: 12.1% — Excellent", "Loan is affordable"],
      "concerns": [],
      "metadata": {
        "creditScoreCategory": "Very Good",
        "dtiRatio": 12.12,
        "monthlyPayment": 507.91,
        "affordable": true
      },
      "completedAt": "2026-05-14T16:05:08Z"
    },
    {
      "agentName": "RiskAnalyst",
      "summary": "Risk score 14 — Low risk. Unsecured personal loan with strong credit and low DTI...",
      "passed": true,
      "score": 86,
      "findings": ["Risk score: 14 (Low)", "Suggested rate: 8.0%", "Unsecured — no collateral"],
      "concerns": [],
      "metadata": {
        "riskLevel": "Low",
        "suggestedRate": 8.0,
        "ltvRatio": 0
      },
      "completedAt": "2026-05-14T16:05:12Z"
    },
    {
      "agentName": "ComplianceOfficer",
      "summary": "KYC passed. AML screening clear. ECOA compliant. HMDA not applicable. Eligible.",
      "passed": true,
      "score": 100,
      "findings": ["KYC: verified", "AML: clear", "ECOA: compliant", "Eligibility: passed"],
      "concerns": [],
      "metadata": {
        "kycPass": true,
        "amlPass": true,
        "ecoaCompliant": true,
        "hmdaCompliant": true,
        "eligibilityPass": true
      },
      "completedAt": "2026-05-14T16:05:16Z"
    },
    {
      "agentName": "LoanOfficer",
      "summary": "Based on a comprehensive review... [full applicant narrative]",
      "passed": true,
      "score": 87,
      "findings": [],
      "concerns": [],
      "metadata": {
        "decisionStatus": "Approved",
        "approvedAmount": 25000.00,
        "interestRate": 8.0,
        "riskLevel": "Low"
      },
      "completedAt": "2026-05-14T16:05:20Z"
    }
  ]
}
```

> **Why is `agentReports` so long?** Each agent writes a full narrative `summary` referencing the applicant's data (income, credit score, etc.). This is the AI's reasoning chain — it is not echoing the request back. The top-level `status`, `reasoning`, `approvedAmount`, and `interestRate` fields are the actionable output you care about.

---

## Local Storage — What Is (and Isn't) Stored

**This API is stateless. No data is written to any database or file.**

| What you might expect | Reality |
|---|---|
| A table of submitted applications | Does not exist — not implemented |
| A log of decisions | Console log only, not persisted |
| CosmosDB entries | Production only — not wired up locally |
| Any file on disk | Nothing written |

Every request is processed in-memory. The only record of a decision is:
1. The JSON response you receive from the API call
2. The console log lines printed while the request was running

In production the `LoanDecision` would be written to CosmosDB (partition key `/tenantId`) and application documents would be stored in Azure Blob Storage. Those integrations are not in scope for this local POC.

---

## Test 1 — Strong Applicant (expect: `Approved`)

Credit 760, income $95k, DTI ≈ 12%, 7 years employment, no documents required.

```json
{
  "applicant": {
    "firstName": "Sarah",
    "lastName": "Johnson",
    "socialSecurityNumberHash": "a3f5c9d2e8b1f7a4c6e9d3b7f2a5c8e1d4b9f6a2c5e8d1b4f7a0c3e6d9b2f5a8",
    "dateOfBirth": "1985-06-15",
    "address": {
      "street": "742 Evergreen Terrace",
      "city": "Springfield",
      "state": "IL",
      "zipCode": "62701"
    },
    "employmentStatus": "FullTime",
    "annualIncome": 95000,
    "monthlyDebtObligations": 450,
    "creditScore": 760,
    "yearsAtCurrentJob": 7,
    "employerName": "Acme Corporation"
  },
  "loanDetails": {
    "loanType": "PersonalLoan",
    "requestedAmount": 25000,
    "termMonths": 60,
    "purpose": "Home renovation"
  },
  "documents": []
}
```

**Expected top-level response fields:**
| Field | Expected value |
|---|---|
| `status` | `"Approved"` |
| `overallScore` | 75–95 |
| `riskLevel` | `"Low"` |
| `approvedAmount` | 25000 |
| `interestRate` | ~8.0 |
| `conditions` | `[]` |

**PowerShell:**
```powershell
$body = @'
{ paste JSON above here }
'@
Invoke-RestMethod -Method Post -Uri "http://localhost:5091/api/loans/apply" `
  -ContentType "application/json" -Body $body | ConvertTo-Json -Depth 10
```

---

## Test 2 — Borderline Applicant (expect: `ConditionallyApproved`)

Credit 618 (Fair), 2 years employment, moderate DTI.

```json
{
  "applicant": {
    "firstName": "Marcus",
    "lastName": "Rivera",
    "socialSecurityNumberHash": "b7d2e5f8a1c4b9e2d5f8a1c4b9e2d5f8a1c4b9e2d5f8a1c4b9e2d5f8a1c4b9e2",
    "dateOfBirth": "1992-11-03",
    "address": {
      "street": "15 Oak Street",
      "unit": "Apt 4B",
      "city": "Austin",
      "state": "TX",
      "zipCode": "78701"
    },
    "employmentStatus": "FullTime",
    "annualIncome": 62000,
    "monthlyDebtObligations": 820,
    "creditScore": 618,
    "yearsAtCurrentJob": 2,
    "employerName": "Riverdale Tech LLC"
  },
  "loanDetails": {
    "loanType": "PersonalLoan",
    "requestedAmount": 18000,
    "termMonths": 48,
    "purpose": "Debt consolidation"
  },
  "documents": [
    {
      "documentId": "00000000-0000-0000-0000-000000000001",
      "type": "PayStub",
      "storageUri": "local://paystub.pdf",
      "fileName": "paystub-nov2024.pdf"
    },
    {
      "documentId": "00000000-0000-0000-0000-000000000002",
      "type": "BankStatement",
      "storageUri": "local://bankstmt.pdf",
      "fileName": "bankstatement-oct2024.pdf"
    }
  ]
}
```

**Expected top-level response fields:**
| Field | Expected value |
|---|---|
| `status` | `"ConditionallyApproved"` |
| `overallScore` | 55–72 |
| `riskLevel` | `"Medium"` |
| `approvedAmount` | 16200–18000 |
| `conditions` | Non-empty (tax returns, income verification) |

> The document plugin uses **mock data** locally — `storageUri` values are never fetched. Passing document references just tells the DocumentAnalyst agent that documents were submitted; it runs its mock extraction regardless.

---

## Test 3 — Hard Denial (expect: `Rejected`)

Credit 465 (below 500 policy floor) + DTI ≈ 55% (above 50% hard ceiling). Both are hard denial triggers.

```json
{
  "applicant": {
    "firstName": "Derek",
    "lastName": "Holloway",
    "socialSecurityNumberHash": "c9e4f7a2d5b8c1e4f7a2d5b8c1e4f7a2d5b8c1e4f7a2d5b8c1e4f7a2d5b8c1e4",
    "dateOfBirth": "1990-03-22",
    "address": {
      "street": "88 Pine Road",
      "city": "Columbus",
      "state": "OH",
      "zipCode": "43201"
    },
    "employmentStatus": "PartTime",
    "annualIncome": 28000,
    "monthlyDebtObligations": 950,
    "creditScore": 465,
    "yearsAtCurrentJob": 1,
    "employerName": "Corner Store Inc."
  },
  "loanDetails": {
    "loanType": "PersonalLoan",
    "requestedAmount": 15000,
    "termMonths": 36,
    "purpose": "Medical expenses"
  },
  "documents": []
}
```

**Expected top-level response fields:**
| Field | Expected value |
|---|---|
| `status` | `"Rejected"` |
| `overallScore` | 0–45 |
| `riskLevel` | `"High"` or `"VeryHigh"` |
| `approvedAmount` | `null` |
| `reasoning` | Contains "credit score" or "DTI" as rejection reason |

**Look for in the console log:**
```
[PLUGIN:Decision] Hard denial — credit score 465 < 500
```

---

## Troubleshooting

| Symptom | Fix |
|---|---|
| `Connection refused` port 11434 | Run `ollama serve` |
| `model "llama3.1" not found` | Run `ollama pull llama3.1` |
| Response is `PendingManualReview` with score 0 | Model returned unparseable JSON; check console for `(no structured JSON report)` lines |
| 503 after ~5 min | Model too slow — try `ollama pull llama3` (smaller) and update `appsettings.Development.json` |
| 400 Bad Request | Missing required field or enum string wrong — valid values: `FullTime`, `PartTime`, `SelfEmployed`, `Unemployed`, `Retired`, `Student` for `employmentStatus`; `PersonalLoan`, `Mortgage`, `AutoLoan`, `BusinessLoan`, `StudentLoan`, `HomeEquityLoan` for `loanType` |
| `agentReports` array is empty | Pipeline errored before agents ran — check `Error` log lines in console |
