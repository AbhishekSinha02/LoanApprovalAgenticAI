# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.



# Azure RAG POC — Claude Code Context

## Project
Enterprise RAG system on Azure. Ingests documents, indexes via AI Search, answers queries via Azure OpenAI.
Stack: C# .NET 8 · ASP.NET Core Minimal API · Semantic Kernel · AKS 1.29 · Terraform 1.7+ · GitHub Actions

## Project Status

This is a new, empty project. No code has been written yet. Update this file as the codebase is built out.

## Project Purpose

Loan Approval Agentic AI — an AI-powered system for automating or assisting with loan approval decisions using agentic (multi-step, tool-using) AI patterns.

## Commands
build:        dotnet build src/AzureRagPoc.sln
test:         dotnet test tests/ --logger trx --collect:"XPlat Code Coverage"
run-local:    dotnet run --project src/Api -- --environment Development
lint:         dotnet format src/ --verify-no-changes
tf-init:      cd infra && terraform init -backend-config=backends/dev.tfvars
tf-plan:      cd infra && terraform plan -var-file=envs/dev.tfvars -out=tfplan
tf-apply:     cd infra && terraform apply tfplan
tf-destroy:   cd infra && terraform destroy -var-file=envs/dev.tfvars
k8s-deploy:   kubectl apply -f k8s/ -n rag-poc
k8s-status:   kubectl get pods,svc,ingress -n rag-poc
aks-creds:    az aks get-credentials --resource-group rg-rag-poc-dev --name aks-rag-poc-dev
ingest:       dotnet run --project src/Ingestion -- --source ./sample-docs
eval:         python scripts/ragas_eval.py --env dev

## Architecture
src/Api/                  — ASP.NET Core Minimal API (query endpoint, health checks)
src/Rag/                  — Retrieval pipeline: chunking, embedding, hybrid search, reranking
src/Ingestion/            — Document ingestion worker: PDF/DOCX → chunks → AI Search index
src/Agents/               — Semantic Kernel agent definitions and planner
src/Shared/               — Domain models, interfaces, DI extensions
tests/Unit/               — xUnit unit tests (no external calls)
tests/Integration/        — xUnit integration tests (uses Azurite + test containers)
infra/modules/            — Terraform reusable modules (aks, openai, ai-search, cosmosdb, keyvault)
infra/envs/               — dev.tfvars / staging.tfvars / prod.tfvars
infra/backends/           — backend.tfvars per environment (Azure Blob state)
k8s/                      — Helm chart + raw manifests (deployment, service, ingress, HPA)
k8s/workload-identity/    — ServiceAccount + federated credential YAML
.github/workflows/        — ci.yml (build+test) · cd.yml (tf plan+apply + k8s deploy)
scripts/                  — ragas_eval.py, load_test.py, seed_index.py

## Azure Resources
| Service            | SKU / Config                                      |
|--------------------|---------------------------------------------------|
| Azure OpenAI       | gpt-4o (eastus2) · text-embedding-3-large         |
| AI Search          | Standard S1 · semantic ranker ON · hybrid BM25+vector |
| AKS                | System pool: Standard_D4s_v5 · User pool: Standard_D8s_v5 |
| CosmosDB           | NoSQL API · session consistency · partitionKey=/tenantId |
| Service Bus        | Standard · dead-letter queue ON · sessions OFF    |
| Key Vault          | Standard · RBAC mode (not access policies)        |
| Container Registry | Basic · geo-replication OFF in dev                |
| Storage Account    | LRS · blob tier · Terraform state + document staging |

## Identity & Security — CRITICAL
- Use Workload Identity (OIDC federated) ONLY — never client secrets or client_id/client_secret in code
- AKS has TWO distinct managed identities: control-plane MI (cluster ops) and kubelet MI (node/pod ops) — never conflate
- Workload Identity requires: oidcIssuerProfile.enabled=true + workloadIdentityEnabled=true on AKS cluster
- Pod annotation: azure.workload.identity/client-id must match the user-assigned MI client ID
- ServiceAccount in k8s/workload-identity/ must have azure.workload.identity/use: "true" label
- Key Vault secrets mounted via CSI driver (Secrets Store) — never via env vars or ConfigMaps
- GitHub Actions CI/CD uses OIDC federated credential — see .github/workflows/cd.yml
- Terraform state backend uses azurerm with MSI auth — see infra/backends/

## Terraform Conventions
- Provider: azurerm >= 3.90 · Use azapi only if resource missing from azurerm
- All resources tagged: environment, project, managed-by=terraform, cost-center
- Modules are self-contained: each has main.tf, variables.tf, outputs.tf, README.md
- Remote state: azurerm backend, container=tfstate, key=<env>/rag-poc.tfstate
- Never run terraform apply manually — always via GitHub Actions CD pipeline
- Sensitive outputs marked sensitive=true — never printed in plan logs

## AI Search Index
- Index name: rag-poc-<env>-index (see infra/modules/ai-search/main.tf)
- Fields: id, content, contentVector, sourceFile, pageNumber, tenantId, lastModified
- contentVector: 3072 dims (text-embedding-3-large) · HNSW algorithm · cosine metric
- Query mode: hybrid (keyword BM25 + vector) with semantic reranking (semantic_configuration="rag-semantic-config")
- Chunking: 512 tokens, 50 token overlap — see src/Ingestion/ChunkingService.cs
- Ingestion pipeline: Storage blob trigger → Document Intelligence → Chunker → Embedder → AI Search indexer

## Semantic Kernel Patterns
- IKernel registered as singleton in src/Shared/Extensions/KernelExtensions.cs
- Use KernelFunction attribute for plugin methods — not manual function descriptors
- Memory: use ISemanticTextMemory with AzureAISearchMemoryStore
- Planner: use FunctionCallingStepwisePlanner for multi-step agent tasks
- Never hardcode deployment names — use IConfiguration["AzureOpenAI:DeploymentName"]

## Constraints & Gotchas
- CosmosDB partition key /tenantId is immutable — schema locked at index creation
- AI Search index schema changes require full re-index — coordinate with data team
- AKS CNI: Azure CNI Overlay — pod IPs are private, no direct external access
- Ingress: NGINX ingress controller installed via Helm (see k8s/ingress/nginx-values.yaml)
- gpt-4o TPM quota: 150K tokens/min in dev — add retry with exponential backoff (Polly)
- text-embedding-3-large has 3072 dims — existing 1536-dim indexes are incompatible
- Document Intelligence: Form Recognizer v3.1 — use prebuilt-layout model for general docs
- .env.local is gitignored — copy .env.example and fill Key Vault references for local dev

## CI/CD Pipeline
- CI (ci.yml): triggers on PR to main · runs build + unit tests + tf validate + tf fmt check
- CD (cd.yml): triggers on merge to main · runs tf plan (PR comment) → manual approval → tf apply → k8s deploy
- Image tag: SHA-based (ghcr.io/org/rag-poc-api:<sha>) — never use :latest
- Rollout: kubectl rollout status deployment/rag-poc-api -n rag-poc (wait for healthy)
- Rollback: kubectl rollout undo deployment/rag-poc-api -n rag-poc

## Local Dev Without Azure
- Use Azurite (Docker) for Storage: AZURE_STORAGE_CONNECTION_STRING=UseDevelopmentStorage=true
- Use Ollama (nomic-embed-text) as embedding fallback when AZURE_OPENAI_ENDPOINT not set
- ChromaDB (Docker) replaces AI Search for local vector store
- docker compose -f compose.local.yml up spins up: API + Azurite + ChromaDB + Ollama
- Set USE_LOCAL_INFRA=true in .env.local to activate local provider implementations
