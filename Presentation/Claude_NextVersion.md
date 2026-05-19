# Claude_NextVersion.md — Loan Approval Agentic AI (v2 + v3)

Extended CLAUDE.md for the RAG + Hybrid Model + Streaming implementation.
This file replaces CLAUDE.md when beginning Phase 2 / Phase 3 work.

---

## Project

Enterprise Loan Approval Agentic AI system.
Multi-agent pipeline with RAG-enriched plugins, hybrid self-hosted/cloud LLM routing,
semantic caching, async streaming, and full Azure production stack.

Stack: C# .NET 8 · ASP.NET Core Minimal API · Semantic Kernel · Azure OpenAI · Azure AI Search ·
AKS 1.29 · Ollama (Llama 3.1 on GPU) · Redis · Service Bus · Terraform 1.7+ · GitHub Actions

---

## Commands

```
build:          dotnet build src/AzureRagPoc.sln
test:           dotnet test tests/ --logger trx --collect:"XPlat Code Coverage"
run-local:      dotnet run --project src/Api -- --environment Development
lint:           dotnet format src/ --verify-no-changes
ingest-docs:    dotnet run --project src/Ingestion -- --source ./knowledge-base --index compliance-index
ingest-credit:  dotnet run --project src/Ingestion -- --source ./credit-data --index credit-index
ingest-risk:    dotnet run --project src/Ingestion -- --source ./rate-tables --index risk-index
tf-init:        cd infra && terraform init -backend-config=backends/dev.tfvars
tf-plan:        cd infra && terraform plan -var-file=envs/dev.tfvars -out=tfplan
tf-apply:       cd infra && terraform apply tfplan
k8s-deploy:     kubectl apply -f k8s/ -n rag-poc
k8s-status:     kubectl get pods,svc,ingress -n rag-poc
ollama-local:   ollama run llama3.1
cache-flush:    redis-cli FLUSHDB   (compliance cache only — use selective pattern flush in prod)
```

---

## Architecture — v2 (RAG + Hybrid)

```
src/Api/                  — ASP.NET Core Minimal API (query + streaming endpoints, health checks)
src/Rag/                  — RAG pipeline: chunking, embedding, hybrid search, reranking
src/Ingestion/            — Document ingestion worker: PDF/DOCX → chunks → AI Search indexes
src/Agents/               — Semantic Kernel agent definitions + orchestrator
  Orchestration/          — LoanApprovalOrchestrator (sequential agents, streaming variant)
  Plugins/                — 5 plugins — each injects ISemanticTextMemory for RAG retrieval
  Extensions/             — KernelExtensions.cs (DI wiring for memory, chat, models)
src/Cache/                — Semantic cache: Redis L1 exact + L2 vector similarity + L3 RAG chunk
src/Shared/               — Domain models, interfaces, DI extensions
tests/Unit/               — xUnit unit tests (no external calls)
tests/Integration/        — xUnit integration tests (Azurite + test containers)
infra/modules/            — Terraform: aks, openai, ai-search, cosmosdb, keyvault, redis, servicebus
infra/envs/               — dev.tfvars / staging.tfvars / prod.tfvars
k8s/                      — Helm chart + manifests (deployment, service, ingress, HPA)
k8s/gpu-pool/             — Llama agent node pool config (Standard_NC4as_T4_v3)
k8s/workload-identity/    — ServiceAccount + federated credential YAML
knowledge-base/           — Source docs for ingestion (regulations, rate tables, precedent decisions)
  compliance/             — CFPB regs, ECOA, HMDA, AML/KYC policies
  credit/                 — Credit bureau report templates, FICO scoring guides
  risk/                   — Market rate tables, LTV guidelines, property valuation methods
  documents/              — Loan doc templates, fraud pattern library
  precedent/              — Past approved/rejected loan decisions (LoanOfficer training data)
.github/workflows/        — ci.yml · cd.yml · ingest.yml (index refresh pipeline)
```

---

## Agent → Model → Hosting Map

| Agent            | Model          | Hosting        | Index                  |
|------------------|----------------|----------------|------------------------|
| DocumentAnalyst  | Llama 3.1 7B   | AKS GPU node   | document-index         |
| CreditAnalyst    | Llama 3.1 8B   | AKS GPU node   | credit-index           |
| RiskAnalyst      | GPT-4o-mini    | Azure OpenAI   | risk-index             |
| ComplianceOfficer| GPT-4o-mini    | Azure OpenAI   | compliance-index       |
| LoanOfficer      | GPT-4o         | Azure OpenAI   | officer-precedent-index|

**Rule:** Never upgrade an agent's model without a cost/accuracy review.
**Rule:** LoanOfficer always uses GPT-4o — it carries all prior reports (highest token load + stakes).

---

## RAG Plugin Pattern

Every plugin MUST follow this pattern:

```csharp
public sealed class CompliancePlugin(
    ISemanticTextMemory memory,          // injected — Azure AI Search backed
    ILogger<CompliancePlugin> logger)
{
    public async Task<string> RunKycCheckAsync(string name, ...)
    {
        // 1. Build semantic query
        var query = $"KYC identity verification requirements for {name}";

        // 2. Retrieve top-5 relevant regulation chunks
        var results = memory.SearchAsync(
            collection: "compliance-index",
            query: query,
            limit: 5,
            minRelevanceScore: 0.75,
            cancellationToken: ct);

        var context = await results
            .Select(r => r.Metadata.Text)
            .AggregateAsync((a, b) => a + "\n---\n" + b);

        // 3. Return enriched context to orchestrator
        // (injected into agent system prompt alongside pre-computed metrics)
        return JsonSerializer.Serialize(new { kycContext = context, applicant = name });
    }
}
```

**Rule:** minRelevanceScore >= 0.75 — never return low-confidence chunks to agents.
**Rule:** limit = 5 chunks max — keep prompt context bounded.
**Rule:** Each plugin uses its own named collection (index) — never cross-pollinate.

---

## Azure AI Search Indexes

| Index                   | Contents                                          | Chunk Size |
|-------------------------|---------------------------------------------------|------------|
| compliance-index        | CFPB regs, ECOA, HMDA, AML/KYC policies          | 512 tokens |
| credit-index            | FICO guides, credit bureau templates, lender policy| 512 tokens |
| risk-index              | Rate tables, LTV guidelines, property valuation   | 256 tokens |
| document-index          | Doc templates, fraud patterns, OCR schema         | 512 tokens |
| officer-precedent-index | Past loan decisions with rationale                | 1024 tokens|

All indexes: text-embedding-3-large (3072 dims) · HNSW · cosine · hybrid BM25+vector · semantic reranker ON.

**Rule:** Index schema changes require full re-index — coordinate with data team.
**Rule:** compliance-index is the most critical — regulation updates trigger immediate re-ingest.

---

## Semantic Kernel DI Registration (v2)

```csharp
// KernelExtensions.cs
services.AddSingleton<ISemanticTextMemory>(sp =>
    new MemoryBuilder()
        .WithAzureOpenAITextEmbeddingGeneration(
            "text-embedding-3-large",
            config["AzureOpenAI:Endpoint"],
            new DefaultAzureCredential())
        .WithMemoryStore(
            new AzureAISearchMemoryStore(
                config["AzureAISearch:Endpoint"],
                new DefaultAzureCredential()))
        .Build());

// Separate kernel per model tier
services.AddKeyedSingleton<Kernel>("llama", (sp, _) =>
    Kernel.CreateBuilder()
        .AddOpenAIChatCompletion(
            modelId: config["Ollama:Model"],       // llama3.1
            endpoint: new Uri(config["Ollama:Endpoint"]),
            apiKey: "ollama")
        .Build());

services.AddKeyedSingleton<Kernel>("gpt-mini", (sp, _) =>
    Kernel.CreateBuilder()
        .AddAzureOpenAIChatCompletion(
            config["AzureOpenAI:MiniDeployment"],  // gpt-4o-mini
            config["AzureOpenAI:Endpoint"],
            new DefaultAzureCredential())
        .Build());

services.AddKeyedSingleton<Kernel>("gpt4o", (sp, _) =>
    Kernel.CreateBuilder()
        .AddAzureOpenAIChatCompletion(
            config["AzureOpenAI:DeploymentName"],  // gpt-4o
            config["AzureOpenAI:Endpoint"],
            new DefaultAzureCredential())
        .Build());
```

**Rule:** Never hardcode model/deployment names — always from IConfiguration.
**Rule:** Use DefaultAzureCredential everywhere — never API keys in code.

---

## Caching Architecture (v2)

Three-layer cache stack — all in src/Cache/:

```
L1 — Exact Match (Redis IDistributedCache)
  Key: SHA256(applicantId + loanAmount + loanType + creditScore)
  TTL: 1 hour
  Hit rate target: ~15%

L2 — Semantic Cache (Redis + text-embedding-3-small)
  Embed incoming request → cosine similarity search in Redis
  Threshold: 0.92 similarity → return cached decision
  TTL: 24 hours
  Hit rate target: ~35-40%

L3 — RAG Chunk Cache (Redis per collection+query)
  Key: SHA256(collection + query + limit)
  TTL: 6 hours (compliance may update)
  Reduces AI Search API calls significantly
```

**Rule:** Compliance cache TTL = 6h max — regulations can update.
**Rule:** LoanOfficer responses should NOT be cached at L2 — each case is unique.
**Rule:** On regulation update event → flush compliance-index cache selectively.

---

## Streaming Architecture (v3)

Replace blocking call at `LoanApprovalOrchestrator.cs:67`:

```csharp
// CURRENT (v1)
var response = await chatService.GetChatMessageContentAsync(history, settings, kernel, ct);

// v3 STREAMING
await foreach (var chunk in chatService.GetStreamingChatMessageContentsAsync(
    history, settings, kernel, ct))
{
    await streamWriter.WriteAsync($"data: {JsonSerializer.Serialize(new {
        agent = agentName,
        token = chunk.Content
    })}\n\n");
    await streamWriter.FlushAsync();
}
```

SSE endpoint: `POST /api/loan/stream` → `Content-Type: text/event-stream`

Event types:
- `agent_start` — agent beginning
- `token` — streaming token chunk
- `agent_complete` — agent JSON report ready
- `decision` — final LoanDecision
- `error` — agent failure (non-fatal, pipeline continues)

**Rule:** Buffer partial JSON until `agent_complete` event — do not parse mid-stream.
**Rule:** Redis pub/sub required for multi-pod streaming (one pod may receive SSE, another runs agent).
**Rule:** Load balancer must support long-lived connections (disable 60s timeout on NGINX ingress).

---

## Azure Resources (v2 additions)

| Service              | SKU / Config                                          |
|----------------------|-------------------------------------------------------|
| Azure OpenAI         | gpt-4o (eastus2) · gpt-4o-mini · text-embedding-3-large |
| AI Search            | Standard S1 · 5 indexes · semantic ranker ON          |
| AKS — System Pool    | Standard_D4s_v5 (API + orchestrator)                  |
| AKS — GPU Pool       | Standard_NC4as_T4_v3 (Llama agents 1-2)               |
| Azure Cache for Redis| Premium P1 · L1/L2/L3 cache layers                    |
| Service Bus          | Standard · priority queue · dead-letter ON            |
| CosmosDB             | NoSQL · /tenantId partition · full audit log          |
| APIM                 | Consumption tier · rate limiting · Entra ID auth      |
| Container Registry   | Basic · Llama image + API image                       |

---

## Identity & Security (unchanged — always enforce)

- Workload Identity (OIDC federated) ONLY — never client_secret
- AKS: two distinct MIs — control-plane MI and kubelet MI — never conflate
- Pod annotation: `azure.workload.identity/client-id` must match user-assigned MI client ID
- Key Vault secrets via CSI driver — never env vars or ConfigMaps
- GitHub Actions: OIDC federated credential — no PATs or passwords
- Terraform state: azurerm backend with MSI auth

---

## Constraints & Gotchas (v2 additions)

- ISemanticTextMemory.SearchAsync returns IAsyncEnumerable — always await with ToListAsync or aggregate
- AzureAISearchMemoryStore collection name = AI Search index name — must match exactly
- text-embedding-3-large: 3072 dims — existing 1536-dim indexes incompatible (full re-index required)
- Llama on AKS: set min-replicas=1 to avoid cold start; T4 GPU ~30s cold start otherwise
- GPT-4o-mini context window = 128K — still enough for all agent prompts
- Streaming + JSON: buffer full agent response before parsing — partial JSON will fail TryParseAgentReport
- Redis L2 semantic cache: use text-embedding-3-small (not large) for cache key embedding — cost vs quality tradeoff acceptable
- APIM: set backend timeout = 120s minimum for LoanOfficer (heaviest prompt)
- Service Bus: set lock duration = 5 minutes for async batch queue — agent pipeline takes 30-60s

---

## Known Debug Issues (from v1)

| Issue | Root Cause | Fix |
|-------|-----------|-----|
| 503 LoanOfficer | CPU-only Ollama + 5 sequential agents exceed timeout | GPU node on AKS + TimeoutMinutes=20 |
| Empty agent response | Llama sees prior context, skips output | Fresh ChatHistory per agent (already fixed in v1) |
| JSON parse fail | Llama wraps output in markdown fences | TryParseAgentReport regex handles ` ```json ``` ` |
| 100% CPU Ollama | No GPU detected | Install CUDA or use llama3.2:1b on CPU |
| ollama not found | Tray app not running | Start Ollama from Start Menu, check tray icon |

---

## CI/CD (v2 additions)

- `ingest.yml` — triggered on push to `knowledge-base/` → re-runs ingestion pipeline per changed index
- Compliance index re-ingest: automatic on push to `knowledge-base/compliance/`
- Cache flush: ingest pipeline calls Redis SCAN + DEL for affected collection keys after re-index
- GPU node pool: separate node pool in Terraform — tagged `workload=llama-agents`
- Spot nodes: `--priority Spot --eviction-policy Delete` for non-critical agents (cost saving)

---

## Local Dev Without Azure (v2)

```bash
# Full local stack
docker compose -f compose.local.yml up
# Starts: API + Azurite + ChromaDB (vector store) + Ollama + Redis

# ChromaDB replaces Azure AI Search locally
USE_LOCAL_INFRA=true  # in .env.local

# Local embedding: nomic-embed-text via Ollama
OLLAMA_EMBEDDING_MODEL=nomic-embed-text  # in .env.local

# Local cache: Redis on localhost:6379 (no SSL)
REDIS_CONNECTION_STRING=localhost:6379   # in .env.local
```

---

## Performance Targets (production)

| Metric                        | Target          |
|-------------------------------|-----------------|
| P99 end-to-end latency        | < 45 seconds    |
| LoanOfficer agent latency     | < 15 seconds    |
| Cache hit (L1+L2) p50         | < 200ms         |
| Throughput                    | 500 concurrent  |
| Availability                  | 99.9%           |
| Semantic cache hit rate       | > 40%           |
| Token usage / request         | < 8,000 tokens  |
| Cost per decision (hybrid)    | < $0.016        |

---

## Cost Targets

| Volume        | All GPT-4o | Hybrid Model | Saving     |
|---------------|------------|--------------|------------|
| 1M requests   | ~$39,000   | ~$15,300     | ~$23,700   |
| 10M requests  | ~$390,000  | ~$153,000    | ~$237,000  |
| AKS GPU/yr    | —          | ~$18,000     | fixed      |
| Net 10M/yr    | —          | —            | ~$219,000  |
