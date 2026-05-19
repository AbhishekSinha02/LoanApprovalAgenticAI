# Debug Notes — Loan Approval Agentic AI

## 5 Debug Challenges Faced

1. **`qwen3-vl:235b` not starting** — mistook it for hardware limit; real cause was Ollama cloud cache timeout blocking startup (`ollama.com:443` unreachable).
2. **`ollama` command not found** — Ollama tray app wasn't running; CLI only works when background app is active.
3. **503 timeout on LoanOfficer** — 5 sequential agent calls on CPU exceeded request timeout; LoanOfficer prompt is heaviest (carries all 4 prior reports).
4. **`bin/` and `obj/` tracked by git** — artifacts were committed before `.gitignore` was effective; required `git rm -r --cached` to untrack.
5. **Ollama on 100% CPU** — no GPU detected; running `llama3.1` on CPU is 10-20x slower, causing per-agent delays of 1min+.

---

## Ollama Setup

| Command | Purpose |
|---------|---------|
| `ollama list` | Show downloaded models |
| `ollama ps` | Show models currently loaded in memory |
| `ollama pull llama3.1` | Pull the correct model for this project |
| `ollama run llama3.1` | Start the model used by this project |
| `ollama stop` | Stop running model |
| `ollama serve` | Start Ollama service manually |
| `ollama --version` | Check installed version |

**Model for this project:** `llama3.1` (~4.7 GB, ~7 GB total with overhead)  
**Endpoint:** `http://localhost:11434`  
**Config files:** `.env.local` → `OLLAMA_MODEL=llama3`, `appsettings.Development.json` → `"Model": "llama3.1"`

---

## Ollama Not Starting — Fix Checklist

```cmd
# 1. Check if Ollama process is running
tasklist | findstr ollama

# 2. Check port 11434
netstat -ano | findstr 11434

# 3. Restart Ollama (Windows — relaunch from Start Menu tray)
# Search "Ollama" in Start → open → wait for tray icon

# 4. If VPN/firewall blocking ollama.com (cloud cache timeout warning)
set OLLAMA_NOPRUNE=1
ollama serve

# 5. Fix corrupted model
ollama pull llama3.1

# 6. If ollama command not found — fix PATH in current terminal
set PATH=%PATH%;%LOCALAPPDATA%\Programs\Ollama
```

---

## Run Project Locally

```cmd
# With Docker
docker compose -f docker-compose.local.yml up

# Without Docker
ollama run llama3.1
dotnet run --project src/Api
```

---

## VS Code Debugging — Key Breakpoints

| File | Line | Purpose |
|------|------|---------|
| `src/Api/Endpoints/LoanApplicationEndpoints.cs` | 51 | Entry — inspect incoming request |
| `src/Agents/Orchestration/LoanApprovalOrchestrator.cs` | 27 | After plugin context — see pre-computed data |
| `src/Agents/Orchestration/LoanApprovalOrchestrator.cs` | 67 | Raw LLM response per agent |
| `src/Agents/Orchestration/LoanApprovalOrchestrator.cs` | 82 | JSON parse result per agent |
| `src/Agents/Orchestration/LoanApprovalOrchestrator.cs` | 46 | Final decision — inspect all 5 agent reports |

---

## Ollama Running on CPU (100%) — Performance Fix

**Means:** No GPU acceleration — model runs on CPU only, 10-20x slower than GPU.

**Check your GPU:**
```cmd
wmic path win32_VideoController get name
nvidia-smi   # NVIDIA only — if works, CUDA may just need reinstalling
```

**If NVIDIA GPU → install CUDA, Ollama auto-detects it.**

**If no dedicated GPU (integrated/none) → switch to smaller model:**
```cmd
ollama pull llama3.2:1b    # ~1GB RAM, fastest
ollama pull llama3.2:3b    # ~2GB RAM, better quality
```
Update `appsettings.Development.json`:
```json
"Model": "llama3.2:1b"
```
Response time drops from ~1min → ~5-10 sec per agent.

---

## Request Timeout — 503 LoanOfficer Cancelled

**Root cause:** 5 agents run sequentially; LoanOfficer (agent 5) gets all 4 prior reports as context — heaviest prompt. Ollama on CPU is slow, total pipeline exceeds request timeout.

**Fix 1 — Increase timeout** in `appsettings.Development.json`:
```json
"LoanApproval": { "TimeoutMinutes": 20 }
```

**Fix 2 — Reduce MaxTokens** in `LoanApprovalOrchestrator.cs` line 31:
```csharp
MaxTokens = 512  // was 1024
```

**Fix 3 — Check GPU usage:**
```cmd
ollama ps   # if shows 100% CPU → GPU not active → much slower
```

---

## Agent Model Strategy (2026-05-19)

| Agent | Recommended Model | Hosting | Rating |
|---|---|---|---|
| **DocumentAnalyst** | Llama 3.1 7B | AKS self-hosted | ⭐⭐⭐ — extraction task, no deep reasoning |
| **CreditAnalyst** | Llama 3.1 8B / Mistral | AKS self-hosted | ⭐⭐⭐ — structured scoring, rules-based |
| **RiskAnalyst** | Llama 3.1 8B or GPT-4o-mini | AKS / Azure OpenAI | ⭐⭐⭐⭐ — needs some reasoning; borderline |
| **ComplianceOfficer** | GPT-4o-mini | Azure OpenAI | ⭐⭐⭐⭐ — legal/regulatory nuance, llama risky |
| **LoanOfficer** | GPT-4o | Azure OpenAI | ⭐⭐⭐⭐⭐ — final verdict, highest stakes |

**Pattern:** Llama on AKS for agents 1-2 (cheap, high volume). GPT-4o-mini for agent 3-4. GPT-4o only for LoanOfficer final decision.
**Why:** Reduces Azure OpenAI cost ~60-70% while keeping accuracy where it matters most.

---

## Claude Code — Token & Session Limit Tips (2026-05-19)

**What Claude Code does to minimize usage:**
- Prompt caching — CLAUDE.md/system prompt cached (5-min TTL), cheaper on cache hits
- Context compaction — auto-summarizes old conversation near context limit
- Tool results trimmed — large file reads/grep outputs truncated before sending to model

**What it does NOT do:**
- Every tool call (Read, Edit, Grep, Bash) adds tokens — no batching
- Long conversations accumulate fast — history grows each turn
- No automatic model downgrade — always uses selected model (Sonnet 4.6)

**Practical tips for this project:**
| Command | Purpose |
|---------|---------|
| `/clear` | Reset context between unrelated tasks |
| `/compact` | Manually summarize before context gets too large |
| Use Grep first | Find specific lines before reading whole files |
| Spawn Explore subagent | Isolates broad search token usage from main context |
| Short focused sessions | Beat one long session for limit efficiency |

---

## Git — Repo Cleanup Done (2026-05-19)

```cmd
# bin/ and obj/ removed from tracking (already in .gitignore)
# .gitignore updated to also exclude: *.lscache, logs2/, commands_history.txt
# .claude/settings.local.json added
git log --oneline -3
```
