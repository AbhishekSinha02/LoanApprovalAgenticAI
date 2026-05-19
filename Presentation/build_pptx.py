from pptx import Presentation
from pptx.util import Inches, Pt, Emu
from pptx.dml.color import RGBColor
from pptx.enum.text import PP_ALIGN
from pptx.util import Inches, Pt
import copy

# ── Colour palette ──────────────────────────────────────────────────────────
NAVY   = RGBColor(0x0A, 0x1F, 0x44)   # slide background / title bars
BLUE   = RGBColor(0x00, 0x6B, 0xFF)   # accent / headings
CYAN   = RGBColor(0x00, 0xD4, 0xFF)   # highlights
WHITE  = RGBColor(0xFF, 0xFF, 0xFF)
LGRAY  = RGBColor(0xF0, 0xF4, 0xF8)   # content background
DGRAY  = RGBColor(0x33, 0x3A, 0x4A)   # body text
GREEN  = RGBColor(0x00, 0xC8, 0x74)
ORANGE = RGBColor(0xFF, 0x8C, 0x00)
RED    = RGBColor(0xE0, 0x2B, 0x2B)
PURPLE = RGBColor(0x7C, 0x3A, 0xED)

prs = Presentation()
prs.slide_width  = Inches(13.33)
prs.slide_height = Inches(7.5)

BLANK = prs.slide_layouts[6]   # totally blank

# ── Helper utilities ────────────────────────────────────────────────────────

def add_rect(slide, l, t, w, h, fill=NAVY, alpha=None):
    shape = slide.shapes.add_shape(1, Inches(l), Inches(t), Inches(w), Inches(h))
    shape.line.fill.background()
    if fill:
        shape.fill.solid()
        shape.fill.fore_color.rgb = fill
    else:
        shape.fill.background()
    return shape

def add_text(slide, text, l, t, w, h,
             size=18, bold=False, color=WHITE, align=PP_ALIGN.LEFT,
             wrap=True, italic=False):
    txb = slide.shapes.add_textbox(Inches(l), Inches(t), Inches(w), Inches(h))
    txb.word_wrap = wrap
    tf = txb.text_frame
    tf.word_wrap = wrap
    p = tf.paragraphs[0]
    p.alignment = align
    run = p.add_run()
    run.text = text
    run.font.size = Pt(size)
    run.font.bold = bold
    run.font.italic = italic
    run.font.color.rgb = color
    return txb

def add_text_block(slide, lines, l, t, w, h,
                   size=13, color=DGRAY, bold_first=False, line_space=1.2):
    """lines = list of strings; first line can be bold."""
    txb = slide.shapes.add_textbox(Inches(l), Inches(t), Inches(w), Inches(h))
    txb.word_wrap = True
    tf = txb.text_frame
    tf.word_wrap = True
    for i, line in enumerate(lines):
        p = tf.add_paragraph() if i > 0 else tf.paragraphs[0]
        p.space_after = Pt(2)
        run = p.add_run()
        run.text = line
        run.font.size = Pt(size)
        run.font.color.rgb = color
        run.font.bold = (bold_first and i == 0)
    return txb

def slide_bg(slide, color=LGRAY):
    bg = add_rect(slide, 0, 0, 13.33, 7.5, fill=color)

def title_bar(slide, title, subtitle=None):
    add_rect(slide, 0, 0, 13.33, 1.1, fill=NAVY)
    add_text(slide, title, 0.3, 0.08, 10, 0.6, size=26, bold=True, color=WHITE)
    if subtitle:
        add_text(slide, subtitle, 0.3, 0.65, 10, 0.4, size=13, color=CYAN, italic=True)

def card(slide, l, t, w, h, heading, bullets, head_color=BLUE, bg=WHITE):
    add_rect(slide, l, t, w, h, fill=bg)
    # heading strip
    add_rect(slide, l, t, w, 0.38, fill=head_color)
    add_text(slide, heading, l+0.1, t+0.04, w-0.2, 0.32, size=13, bold=True, color=WHITE)
    add_text_block(slide, bullets, l+0.12, t+0.42, w-0.24, h-0.5, size=11, color=DGRAY)

def arrow(slide, x1, y1, x2, y2, color=BLUE, width=Pt(2)):
    from pptx.util import Inches
    connector = slide.shapes.add_connector(1,
        Inches(x1), Inches(y1), Inches(x2), Inches(y2))
    connector.line.color.rgb = color
    connector.line.width = width

# ══════════════════════════════════════════════════════════════════════════════
# SLIDE 1 — Title
# ══════════════════════════════════════════════════════════════════════════════
s = prs.slides.add_slide(BLANK)
add_rect(s, 0, 0, 13.33, 7.5, fill=NAVY)
add_rect(s, 0, 2.8, 13.33, 2.2, fill=BLUE)
add_text(s, "Loan Approval Agentic AI", 0.5, 2.95, 12.3, 1.0,
         size=40, bold=True, color=WHITE, align=PP_ALIGN.CENTER)
add_text(s, "Enterprise Multi-Agent Pipeline · RAG · Hybrid Models · Azure Cloud",
         0.5, 3.95, 12.3, 0.5, size=16, color=CYAN, align=PP_ALIGN.CENTER, italic=True)
add_text(s, "Current Build → Production Roadmap", 0.5, 4.6, 12.3, 0.4,
         size=13, color=LGRAY, align=PP_ALIGN.CENTER)
add_text(s, "Stack: .NET 8 · Semantic Kernel · Azure OpenAI · Azure AI Search · AKS · Ollama",
         0.5, 6.8, 12.3, 0.4, size=11, color=RGBColor(0xAA,0xBB,0xCC),
         align=PP_ALIGN.CENTER, italic=True)

# ══════════════════════════════════════════════════════════════════════════════
# SLIDE 2 — Project Overview Mind Map
# ══════════════════════════════════════════════════════════════════════════════
s = prs.slides.add_slide(BLANK)
slide_bg(s)
title_bar(s, "Project Overview", "What this system does — mind map style")

# Center node
add_rect(s, 5.4, 2.8, 2.5, 0.8, fill=NAVY)
add_text(s, "Loan Approval\nAgentic AI", 5.4, 2.8, 2.5, 0.8,
         size=12, bold=True, color=WHITE, align=PP_ALIGN.CENTER)

nodes = [
    (0.3,  1.3, 2.8, 2.0, "What It Does",
     ["• Automates loan decisions", "• 5 specialist AI agents", "• Rule + LLM hybrid", "• Full audit trail"]),
    (0.3,  4.0, 2.8, 2.2, "Current Stack",
     ["• .NET 8 / Minimal API", "• Semantic Kernel", "• Ollama (local dev)", "• llama3.1 model"]),
    (4.8,  1.1, 3.7, 1.4, "Agent Pipeline",
     ["Doc → Credit → Risk → Compliance → LoanOfficer"]),
    (9.2,  1.3, 3.8, 2.0, "Production Target",
     ["• Azure OpenAI (GPT-4o)", "• Azure AI Search (RAG)", "• AKS self-hosted Llama", "• Workload Identity"]),
    (9.2,  4.0, 3.8, 2.2, "Business Value",
     ["• 60-70% cost saving (hybrid)", "• <30s decision time", "• Regulatory compliance", "• Explainable AI"]),
    (4.8,  5.5, 3.7, 1.4, "Key Differentiator",
     ["RAG per plugin = real data decisions, not hardcoded rules"]),
]
for lft, top, wid, hgt, heading, bullets in nodes:
    card(s, lft, top, wid, hgt, heading, bullets)

# ══════════════════════════════════════════════════════════════════════════════
# SLIDE 3 — Current Architecture
# ══════════════════════════════════════════════════════════════════════════════
s = prs.slides.add_slide(BLANK)
slide_bg(s)
title_bar(s, "Current Architecture (v1)", "Request → Plugin Pre-compute → 5 Agent LLM Calls → Decision")

# Flow boxes
boxes = [
    (0.3,  1.4, 2.2, "Client\n(HTTP POST)", BLUE),
    (2.9,  1.4, 2.2, "ASP.NET Core\nMinimal API", NAVY),
    (5.5,  1.4, 2.2, "Loan Approval\nOrchestrator", PURPLE),
    (8.1,  1.4, 2.2, "Plugin Layer\n(Pre-compute)", GREEN),
    (10.7, 1.4, 2.2, "Ollama /\nAzure OpenAI", ORANGE),
]
for x, y, w, label, col in boxes:
    add_rect(s, x, y, w, 1.0, fill=col)
    add_text(s, label, x, y, w, 1.0, size=12, bold=True,
             color=WHITE, align=PP_ALIGN.CENTER)

# arrows between boxes
for x in [2.5, 5.1, 7.7, 10.3]:
    add_rect(s, x, 1.82, 0.4, 0.15, fill=BLUE)
    add_text(s, "▶", x+0.05, 1.78, 0.3, 0.2, size=13, color=WHITE)

# 5 agent boxes
agent_labels = ["1. DocumentAnalyst", "2. CreditAnalyst", "3. RiskAnalyst",
                "4. ComplianceOfficer", "5. LoanOfficer (Final)"]
agent_colors = [GREEN, BLUE, ORANGE, PURPLE, RED]
for i, (lbl, col) in enumerate(zip(agent_labels, agent_colors)):
    x = 0.3 + i * 2.6
    add_rect(s, x, 3.1, 2.3, 0.65, fill=col)
    add_text(s, lbl, x+0.05, 3.1, 2.2, 0.65, size=10, bold=True,
             color=WHITE, align=PP_ALIGN.CENTER)

add_text(s, "Sequential Agent Calls (line 67: chatService.GetChatMessageContentAsync)",
         0.3, 3.85, 12.7, 0.35, size=11, color=DGRAY, italic=True)

# bottom note boxes
notes = [
    (0.3,  4.4, 4.0, "Plugin Pre-compute (No LLM)",
     ["• Math: DTI, LTV, monthly payment", "• Rules: credit score brackets",
      "• Mock: KYC/AML/Doc checks", "• Output: structured JSON context"]),
    (4.6,  4.4, 4.0, "LLM Reasoning Layer",
     ["• Each agent: fresh ChatHistory", "• System prompt = role + rules",
      "• User prompt = pre-computed data", "• Output: JSON report"]),
    (8.9,  4.4, 4.2, "Decision Assembly",
     ["• LoanOfficer sees all 4 reports", "• Weighted scoring (credit 35%)",
      "• Hard denial rules enforced", "• Returns LoanDecision object"]),
]
for l, t, w, h, bullets in [(n[0],n[1],n[2],1.8,n[3:]) for n in notes]:
    card(s, l, t, w, 1.8, notes[[n[0] for n in notes].index(l)][3], notes[[n[0] for n in notes].index(l)][4])

# redo notes cleanly
for l, t, w, heading, bullets in [
    (0.3, 4.4, 4.0, "Plugin Pre-compute (No LLM)",
     ["• Math: DTI, LTV, monthly payment", "• Rules: credit score brackets",
      "• Mock: KYC/AML/Doc checks", "• Output: structured JSON context"]),
    (4.6, 4.4, 4.0, "LLM Reasoning Layer",
     ["• Each agent: fresh ChatHistory", "• System prompt = role + rules",
      "• User prompt = pre-computed data", "• Output: JSON report"]),
    (8.9, 4.4, 4.2, "Decision Assembly",
     ["• LoanOfficer sees all 4 reports", "• Weighted scoring (credit 35%)",
      "• Hard denial rules enforced", "• Returns LoanDecision object"]),
]:
    card(s, l, t, w, 1.8, heading, bullets)

# ══════════════════════════════════════════════════════════════════════════════
# SLIDE 4 — Sequence Diagram
# ══════════════════════════════════════════════════════════════════════════════
s = prs.slides.add_slide(BLANK)
slide_bg(s)
title_bar(s, "Agent Pipeline — Sequence Flow", "Request lifecycle through all 5 agents")

actors = ["Client", "API", "Orchestrator", "Plugins", "LLM"]
colors = [BLUE, NAVY, PURPLE, GREEN, ORANGE]
xs = [0.9, 2.8, 4.9, 7.1, 9.8]

for x, label, col in zip(xs, actors, colors):
    add_rect(s, x, 1.15, 1.5, 0.5, fill=col)
    add_text(s, label, x, 1.15, 1.5, 0.5, size=12, bold=True,
             color=WHITE, align=PP_ALIGN.CENTER)
    add_rect(s, x+0.68, 1.65, 0.14, 5.2, fill=RGBColor(0xCC,0xDD,0xEE))

steps = [
    (0.9, 2.8,  2.8, 2.8,  "POST /api/loan", BLUE),
    (2.8, 2.8,  4.9, 2.8,  "ProcessApplicationAsync()", NAVY),
    (4.9, 3.2,  7.1, 3.2,  "ComputePluginContextAsync()", PURPLE),
    (7.1, 3.2,  4.9, 3.2,  "JSON metrics (DTI,LTV,Risk…)", GREEN),
    (4.9, 3.8,  9.8, 3.8,  "Agent 1-4: GetChatMessageContentAsync()", PURPLE),
    (9.8, 4.2,  4.9, 4.2,  "AgentReport JSON x4", ORANGE),
    (4.9, 4.7,  9.8, 4.7,  "LoanOfficer: all reports + app data", RED),
    (9.8, 5.1,  4.9, 5.1,  "Final verdict JSON", ORANGE),
    (4.9, 5.6,  2.8, 5.6,  "BuildFinalDecision()", PURPLE),
    (2.8, 5.6,  0.9, 5.6,  "LoanDecision response", NAVY),
]
for x1,y1,x2,y2,label,col in steps:
    connector = s.shapes.add_connector(1,
        Inches(x1+0.75), Inches(y1), Inches(x2+0.75), Inches(y2))
    connector.line.color.rgb = col
    connector.line.width = Pt(1.5)
    mid_x = (x1+x2)/2 + 0.1
    add_text(s, label, min(x1,x2)+0.85, y1-0.22, abs(x2-x1)-0.1, 0.22,
             size=9, color=DGRAY, italic=True)

add_text(s, "⚡ All LLM calls funnel through line 67 of LoanApprovalOrchestrator.cs",
         0.3, 6.9, 12.7, 0.35, size=11, bold=True, color=RED)

# ══════════════════════════════════════════════════════════════════════════════
# SLIDE 5 — Hybrid Model Strategy
# ══════════════════════════════════════════════════════════════════════════════
s = prs.slides.add_slide(BLANK)
slide_bg(s)
title_bar(s, "Hybrid Model Strategy", "Self-hosted Llama on AKS + Azure OpenAI GPT — right model, right task")

headers = ["Agent", "Model", "Hosting", "Why", "Rating"]
col_w   = [2.5, 2.2, 2.2, 4.5, 1.4]
col_x   = [0.3, 2.9, 5.2, 7.5, 12.1]

add_rect(s, 0.3, 1.2, 12.8, 0.45, fill=NAVY)
for hdr, cx, cw in zip(headers, col_x, col_w):
    add_text(s, hdr, cx+0.05, 1.22, cw-0.1, 0.4, size=12, bold=True,
             color=WHITE, align=PP_ALIGN.CENTER)

rows = [
    ("DocumentAnalyst",  "Llama 3.1 7B",    "AKS GPU node",    "Extraction task — no deep reasoning needed",     "⭐⭐⭐",   LGRAY),
    ("CreditAnalyst",    "Llama 3.1 8B",    "AKS GPU node",    "Structured scoring, rules-based, llama handles well","⭐⭐⭐", WHITE),
    ("RiskAnalyst",      "GPT-4o-mini",     "Azure OpenAI",    "Borderline — needs some reasoning on risk nuance","⭐⭐⭐⭐", LGRAY),
    ("ComplianceOfficer","GPT-4o-mini",     "Azure OpenAI",    "Legal/regulatory nuance — llama risky here",     "⭐⭐⭐⭐", WHITE),
    ("LoanOfficer",      "GPT-4o",          "Azure OpenAI",    "Final verdict — highest stakes, must be best model","⭐⭐⭐⭐⭐",LGRAY),
]
for i,(agent,model,host,why,rating,bg) in enumerate(rows):
    y = 1.75 + i*0.75
    add_rect(s, 0.3, y, 12.8, 0.72, fill=bg)
    for val, cx, cw in zip([agent,model,host,why,rating], col_x, col_w):
        col = RED if "LoanOfficer" in agent and val==agent else DGRAY
        add_text(s, val, cx+0.05, y+0.1, cw-0.1, 0.55, size=11,
                 color=col, bold=("LoanOfficer" in agent))

# bottom summary
add_rect(s, 0.3, 5.65, 12.8, 0.7, fill=NAVY)
add_text(s, "💰  Cost Impact: Routing 4 of 5 agents to self-hosted Llama saves ~60-70% of Azure OpenAI spend",
         0.5, 5.7, 12.4, 0.35, size=13, bold=True, color=CYAN)
add_text(s, "1M requests: All GPT-4o ≈ $39,000  →  Hybrid ≈ $15,300  |  10M annual saving ≈ $237,000",
         0.5, 6.05, 12.4, 0.3, size=11, color=WHITE)

# ══════════════════════════════════════════════════════════════════════════════
# SLIDE 6 — Cost Optimisation
# ══════════════════════════════════════════════════════════════════════════════
s = prs.slides.add_slide(BLANK)
slide_bg(s)
title_bar(s, "Cost Optimisation at Scale", "1M → 10M requests — all-GPT vs hybrid vs extended hybrid")

add_rect(s, 0.3, 1.25, 12.8, 0.45, fill=NAVY)
for hdr, cx, cw in zip(["Scenario","1M Requests","10M Requests","AKS Infra/yr","Net Annual Saving"],
                        [0.3,3.0,5.7,8.4,10.8],[2.6,2.6,2.6,2.3,2.4]):
    add_text(s, hdr, cx+0.05, 1.27, cw-0.1, 0.4, size=12, bold=True,
             color=WHITE, align=PP_ALIGN.CENTER)

cost_rows = [
    ("All GPT-4o (5 agents)",     "$39,000",  "$390,000", "None",    "Baseline",           WHITE),
    ("Hybrid (2 AKS + 3 GPT)",    "$15,300",  "$153,000", "~$18k",   "~$219,000 (~56%)",   LGRAY),
    ("Extended (8 AKS + 2 GPT)",  "$16,000",  "$160,000", "~$36k",   "~$194,000 (~50%)",   WHITE),
    ("10-agent Extended Hybrid",  "$16,000",  "$160,000", "~$36k",   "~$584,000 net",       RGBColor(0xE8,0xFF,0xEE)),
]
for i,(scenario,c1m,c10m,infra,saving,bg) in enumerate(cost_rows):
    y = 1.78 + i*0.72
    add_rect(s, 0.3, y, 12.8, 0.68, fill=bg)
    for val,cx,cw in zip([scenario,c1m,c10m,infra,saving],
                         [0.3,3.0,5.7,8.4,10.8],[2.6,2.6,2.6,2.3,2.4]):
        add_text(s, val, cx+0.08, y+0.1, cw-0.16, 0.5, size=11, color=DGRAY)

# tips
add_rect(s, 0.3, 4.8, 6.0, 2.4, fill=WHITE)
add_rect(s, 0.3, 4.8, 6.0, 0.38, fill=GREEN)
add_text(s, "Cost-Speed Tactics", 0.4, 4.82, 5.8, 0.34, size=12, bold=True, color=WHITE)
add_text_block(s, [
    "• Small models (GPT-4o-mini) for agents 3-4",
    "• Semantic caching: identical loan profiles hit cache",
    "• Batch low-priority applications off-peak",
    "• Token budgeting: MaxTokens=512 for early agents",
    "• Prompt compression: summarise prior reports",
], 0.4, 5.22, 5.7, 1.9, size=11, color=DGRAY)

add_rect(s, 6.6, 4.8, 6.5, 2.4, fill=WHITE)
add_rect(s, 6.6, 4.8, 6.5, 0.38, fill=PURPLE)
add_text(s, "AKS GPU Scaling", 6.7, 4.82, 6.3, 0.34, size=12, bold=True, color=WHITE)
add_text_block(s, [
    "• Standard_NC4as_T4_v3 (~$0.50/hr) per node",
    "• HPA: scale 2→10 pods under load",
    "• min-replicas=1: no cold start",
    "• Spot nodes for non-critical agents (70% cheaper)",
    "• AKS GPU fixed cost amortised at 3k+ req/day",
], 6.7, 5.22, 6.3, 1.9, size=11, color=DGRAY)

# ══════════════════════════════════════════════════════════════════════════════
# SLIDE 7 — RAG Architecture per Plugin
# ══════════════════════════════════════════════════════════════════════════════
s = prs.slides.add_slide(BLANK)
slide_bg(s)
title_bar(s, "RAG Integration — Per Plugin Architecture",
          "Replace hardcoded rules with real knowledge retrieval from Azure AI Search")

# Flow: Application → Plugin → AI Search → Chunks → Prompt → LLM
flow = [
    (0.3,  2.2, 1.8, "Loan\nApplication", BLUE),
    (2.5,  2.2, 1.8, "Plugin\n(e.g. Compliance)", PURPLE),
    (4.7,  2.2, 1.8, "Embed\nQuery", NAVY),
    (6.9,  2.2, 1.8, "Azure\nAI Search", ORANGE),
    (9.1,  2.2, 1.8, "Relevant\nChunks", GREEN),
    (11.1, 2.2, 1.8, "LLM\n(Agent)", RED),
]
for x, y, w, lbl, col in flow:
    add_rect(s, x, y, w, 0.9, fill=col)
    add_text(s, lbl, x, y, w, 0.9, size=11, bold=True,
             color=WHITE, align=PP_ALIGN.CENTER)
for x in [2.1, 4.3, 6.5, 8.7, 10.9]:
    add_text(s, "→", x, 2.48, 0.4, 0.35, size=18, bold=True, color=NAVY)

# Per-plugin RAG sources
plugin_cards = [
    (0.3,  3.5, 2.5, "DocumentAnalyst RAG",
     ["Knowledge Base:", "• Loan doc templates", "• Fraud pattern library", "• OCR extraction rules", "• Document schema registry"], BLUE),
    (3.1,  3.5, 2.5, "CreditAnalyst RAG",
     ["Knowledge Base:", "• Credit bureau reports", "• Historical loan perf", "• FICO scoring guides", "• Lender policy docs"], GREEN),
    (5.9,  3.5, 2.5, "RiskAnalyst RAG",
     ["Knowledge Base:", "• Market rate indexes", "• Property valuations", "• Fraud pattern DB", "• Historical defaults"], ORANGE),
    (8.7,  3.5, 2.5, "ComplianceOfficer RAG",
     ["Knowledge Base: ⭐ MOST CRITICAL", "• CFPB regulations", "• ECOA / HMDA rulebooks", "• State lending laws", "• AML/KYC policies"], RED),
    (0.3,  5.7, 5.5, "LoanOfficer RAG",
     ["Knowledge Base:", "• Past approved/rejected decisions (precedent)", "• Underwriting guidelines", "• Loan product catalogs", "• Risk appetite statements"], PURPLE),
    (6.1,  5.7, 6.9, "Azure AI Search Index per Domain",
     ["• Separate index per plugin domain (compliance-index, credit-index…)",
      "• Hybrid search: BM25 keyword + vector (3072-dim embeddings)",
      "• Semantic reranking for top-K chunks",
      "• ISemanticTextMemory injection into each plugin class"], NAVY),
]
for l, t, w, heading, bullets, col in plugin_cards:
    card(s, l, t, w, 1.9, heading, bullets, head_color=col)

# ══════════════════════════════════════════════════════════════════════════════
# SLIDE 8 — Azure Stack for RAG
# ══════════════════════════════════════════════════════════════════════════════
s = prs.slides.add_slide(BLANK)
slide_bg(s)
title_bar(s, "Azure Stack — Full RAG Architecture",
          "End-to-end Azure services powering the production system")

services = [
    (0.3,  1.3, 3.8, "Ingestion Pipeline",
     ["Azure Blob Storage", "  → Document Intelligence (Form Recognizer v3.1)",
      "  → Chunking Service (512 tokens, 50 overlap)",
      "  → text-embedding-3-large (3072 dims)",
      "  → AI Search Indexer"], BLUE),
    (4.4,  1.3, 4.3, "Query / RAG Pipeline",
     ["1. Plugin query → embed via Azure OpenAI",
      "2. Hybrid search: BM25 + vector on AI Search",
      "3. Semantic reranker → top-K chunks",
      "4. Chunks injected into agent system prompt",
      "5. LLM generates grounded response"], PURPLE),
    (9.0,  1.3, 4.0, "Azure OpenAI",
     ["• GPT-4o  → LoanOfficer (final decision)",
      "• GPT-4o-mini → Risk + Compliance agents",
      "• text-embedding-3-large → all embeddings",
      "• 150K TPM quota (dev) + Polly retry",
      "• Managed Identity auth (no keys)"], ORANGE),
    (0.3,  4.0, 3.8, "Azure AI Search",
     ["• Standard S1 · semantic ranker ON",
      "• Hybrid: BM25 + vector (cosine)",
      "• Index: id, content, contentVector,",
      "  sourceFile, tenantId, lastModified",
      "• One index per plugin domain"], GREEN),
    (4.4,  4.0, 4.3, "Identity & Security",
     ["• Workload Identity (OIDC federated)",
      "• AKS: control-plane MI + kubelet MI",
      "• Key Vault (CSI driver) — no env secrets",
      "• Pod annotation: workload.identity/client-id",
      "• GitHub Actions OIDC for CI/CD"], RED),
    (9.0,  4.0, 4.0, "Supporting Services",
     ["• CosmosDB: audit log (partitionKey=/tenantId)",
      "• Service Bus: async ingestion queue",
      "• Container Registry: Llama + API images",
      "• Storage Account: doc staging + tf state",
      "• Azure Monitor + Log Analytics"], NAVY),
]
for l, t, w, heading, bullets, col in services:
    card(s, l, t, w, 2.4, heading, bullets, head_color=col)

add_rect(s, 0.3, 6.55, 12.8, 0.7, fill=NAVY)
add_text(s,
    "AKS: Llama 3.1 (agents 1-2) on GPU node pool (Standard_NC4as_T4_v3)  ·  "
    "API on Standard_D4s_v5  ·  HPA min=1 max=10  ·  Workload Identity on all pods",
    0.5, 6.6, 12.5, 0.6, size=11, color=CYAN)

# ══════════════════════════════════════════════════════════════════════════════
# SLIDE 9 — High Volume RAG Architecture
# ══════════════════════════════════════════════════════════════════════════════
s = prs.slides.add_slide(BLANK)
slide_bg(s)
title_bar(s, "High-Volume RAG Architecture",
          "Patterns for 10M+ requests — caching, fan-out, async queuing")

layers = [
    (0.3, 1.3, 12.8, 0.9, "ENTRY LAYER", NAVY,
     "API Gateway (APIM)  ·  Rate Limiting  ·  Auth (Entra ID)  ·  Request Routing"),
    (0.3, 2.4, 12.8, 0.9, "CACHE LAYER", PURPLE,
     "Semantic Cache (Redis + embedding similarity)  ·  Hit rate target >40%  ·  TTL=24h for low-risk profiles"),
    (0.3, 3.5, 12.8, 0.9, "QUEUE LAYER", BLUE,
     "Service Bus  ·  Priority queue: urgent (sync) vs batch (async)  ·  Dead-letter handling  ·  Sessions OFF"),
    (0.3, 4.6, 12.8, 0.9, "AGENT LAYER", GREEN,
     "AKS pods per agent type  ·  HPA per agent  ·  Llama pool (agents 1-2)  ·  GPT pool (agents 3-5)"),
    (0.3, 5.7, 12.8, 0.9, "RAG LAYER", ORANGE,
     "AI Search (per-domain index)  ·  Embedding cache  ·  Vector + BM25 hybrid  ·  Semantic reranker"),
]
for l, t, w, h, lbl, col, desc in layers:
    add_rect(s, l, t, w, h, fill=col)
    add_text(s, lbl, l+0.15, t+0.05, 2.5, 0.38, size=11, bold=True, color=WHITE)
    add_text(s, desc, l+2.7, t+0.18, w-2.85, 0.55, size=11, color=WHITE)

add_rect(s, 0.3, 6.75, 12.8, 0.5, fill=LGRAY)
add_text_block(s, [
    "Key levers: Semantic cache kills 40%+ LLM calls  ·  "
    "Batch queue for non-urgent apps  ·  "
    "Spot nodes cut GPU cost 70%  ·  "
    "Per-agent HPA avoids over-provisioning"
], 0.5, 6.8, 12.5, 0.4, size=11, color=DGRAY)

# ══════════════════════════════════════════════════════════════════════════════
# SLIDE 10 — Semantic Caching Architecture
# ══════════════════════════════════════════════════════════════════════════════
s = prs.slides.add_slide(BLANK)
slide_bg(s)
title_bar(s, "Caching Architecture — Semantic + Response Cache",
          "Avoid redundant LLM calls for similar loan profiles")

# Cache flow
cache_flow = [
    (0.3,  2.0, 2.2, "Incoming\nRequest", BLUE),
    (2.9,  2.0, 2.2, "Embed\nRequest", NAVY),
    (5.5,  2.0, 2.2, "Redis\nVector Search", PURPLE),
    (8.1,  1.3, 2.2, "Cache HIT\n→ Return", GREEN),
    (8.1,  2.7, 2.2, "Cache MISS\n→ LLM", RED),
    (10.8, 2.0, 2.2, "Store in\nCache", ORANGE),
]
for x, y, w, lbl, col in cache_flow:
    add_rect(s, x, y, w, 0.9, fill=col)
    add_text(s, lbl, x, y, w, 0.9, size=11, bold=True,
             color=WHITE, align=PP_ALIGN.CENTER)

cards_data = [
    (0.3, 3.4, 4.0, "L1 — Exact Match Cache",
     ["• Redis exact hash of input params", "• TTL: 1 hour", "• Use case: duplicate submissions",
      "• Hit rate: ~15%", "• Implementation: IDistributedCache"], BLUE),
    (4.6, 3.4, 4.0, "L2 — Semantic Cache",
     ["• Embed request → cosine similarity >0.92", "• TTL: 24 hours", "• Use case: similar profiles",
      "• Hit rate: ~35-40%", "• Stack: Redis + text-embedding-3-small"], PURPLE),
    (8.9, 3.4, 4.2, "L3 — RAG Chunk Cache",
     ["• Cache AI Search results per query", "• TTL: 6 hours (regulation updates)", "• Use case: compliance lookups",
      "• Reduces AI Search cost significantly", "• Azure Cache for Redis Premium"], ORANGE),
    (0.3, 5.7, 5.8, "Cache Strategy Mind Map",
     ["• High-value cache: ComplianceOfficer (same regs for all apps)",
      "• Low-value cache: LoanOfficer (each case unique)",
      "• Invalidation: regulation update → flush compliance cache",
      "• Monitoring: cache hit rate dashboard in Grafana"], GREEN),
    (6.4, 5.7, 6.6, "Expected Impact at 1M Requests",
     ["• L1 saves: ~15,000 LLM calls → ~$600",
      "• L2 saves: ~350,000 LLM calls → ~$14,000",
      "• L3 saves: ~40% AI Search queries → ~$800",
      "• Total cache saving: ~$15,400 per 1M requests",
      "• ROI: Redis Premium ~$800/mo vs $15k+ saving"], NAVY),
]
for l, t, w, heading, bullets, col in cards_data:
    card(s, l, t, w, 2.0, heading, bullets, head_color=col)

# ══════════════════════════════════════════════════════════════════════════════
# SLIDE 11 — Streaming Architecture
# ══════════════════════════════════════════════════════════════════════════════
s = prs.slides.add_slide(BLANK)
slide_bg(s)
title_bar(s, "Streaming Architecture — Future Extension",
          "Server-Sent Events + async token streaming replacing request-response")

add_rect(s, 0.3, 1.25, 12.8, 0.55, fill=RGBColor(0x1A,0x3A,0x6B))
add_text(s, "Current: blocking await chatService.GetChatMessageContentAsync()  →  "
            "Future: GetStreamingChatMessageContentsAsync() + SSE to client",
         0.5, 1.3, 12.5, 0.45, size=12, bold=True, color=CYAN)

stream_cards = [
    (0.3, 2.0, 3.8, "API Change",
     ["• Replace GetChatMessageContentAsync",
      "• Use GetStreamingChatMessageContentsAsync",
      "• IAsyncEnumerable<StreamingChatMessageContent>",
      "• ASP.NET: Response.Body write loop",
      "• Content-Type: text/event-stream"], BLUE),
    (4.4, 2.0, 4.0, "Client Experience",
     ["• Agent tokens appear as they generate",
      "• Per-agent progress events sent to UI",
      "• 'DocumentAnalyst thinking…' indicator",
      "• Final decision streamed last",
      "• Perceived latency drops ~60%"], GREEN),
    (8.7, 2.0, 4.3, "Architecture Changes",
     ["• SignalR Hub OR SSE endpoint",
      "• Redis pub/sub for multi-pod streaming",
      "• Cancel token propagation per agent",
      "• Partial JSON buffer until complete",
      "• Heartbeat every 15s to keep connection"], PURPLE),
    (0.3, 4.35, 3.8, "Streaming Code Pattern",
     ["await foreach (var chunk in",
      "  chatSvc.GetStreamingChatMessageContentsAsync(",
      "    history, settings, kernel, ct))",
      "{",
      "  yield return chunk.Content;",
      "}"], NAVY),
    (4.4, 4.35, 4.0, "Agent Progress Events",
     ["event: agent_start",
      "data: {agent:'DocumentAnalyst'}",
      "",
      "event: token",
      "data: {text:'Docs look authentic...'}",
      "",
      "event: agent_complete",
      "data: {agent:'DocumentAnalyst',score:88}"], ORANGE),
    (8.7, 4.35, 4.3, "Trade-offs",
     ["✅ Better UX — live feedback",
      "✅ Early cancellation if hard-fail",
      "✅ Partial results usable",
      "⚠️ JSON parsing harder (partial chunks)",
      "⚠️ Load balancer must support SSE",
      "⚠️ Redis needed for multi-pod fan-out"], RED),
]
for l, t, w, heading, bullets, col in stream_cards:
    card(s, l, t, w, 2.15, heading, bullets, head_color=col)

# ══════════════════════════════════════════════════════════════════════════════
# SLIDE 12 — AKS & Infra Overview (single slide)
# ══════════════════════════════════════════════════════════════════════════════
s = prs.slides.add_slide(BLANK)
slide_bg(s)
title_bar(s, "AKS & Infrastructure Overview",
          "Single-slide infra summary — Azure CNI Overlay · Workload Identity · Helm")

add_rect(s, 0.3, 1.25, 5.8, 5.2, fill=WHITE)
add_rect(s, 0.3, 1.25, 5.8, 0.4, fill=NAVY)
add_text(s, "AKS Cluster", 0.45, 1.28, 5.5, 0.35, size=12, bold=True, color=WHITE)
add_text_block(s, [
    "System Pool: Standard_D4s_v5  (API + orchestrator)",
    "User Pool (GPU): Standard_NC4as_T4_v3  (Llama agents)",
    "CNI: Azure CNI Overlay — pod IPs private",
    "Ingress: NGINX controller via Helm",
    "HPA: min=1 max=10 per agent deployment",
    "Workload Identity: OIDC federated credential",
    "  → Pod annotation: workload.identity/client-id",
    "  → ServiceAccount label: workload.identity/use=true",
    "Secrets: Key Vault CSI driver mount (never env vars)",
    "Image tag: SHA-based from GHCR (never :latest)",
    "Rollout: kubectl rollout status (wait healthy)",
    "Rollback: kubectl rollout undo",
], 0.45, 1.72, 5.5, 4.6, size=10.5, color=DGRAY)

add_rect(s, 6.4, 1.25, 6.7, 2.45, fill=WHITE)
add_rect(s, 6.4, 1.25, 6.7, 0.4, fill=BLUE)
add_text(s, "Terraform Modules", 6.55, 1.28, 6.4, 0.35, size=12, bold=True, color=WHITE)
add_text_block(s, [
    "infra/modules/: aks · openai · ai-search · cosmosdb · keyvault",
    "Remote state: azurerm backend · Azure Blob · key=<env>/rag-poc.tfstate",
    "Tags: environment · project · managed-by=terraform · cost-center",
    "Never run terraform apply manually — CD pipeline only",
    "Sensitive outputs: sensitive=true — never in plan logs",
], 6.55, 1.72, 6.4, 1.85, size=10.5, color=DGRAY)

add_rect(s, 6.4, 3.9, 6.7, 2.55, fill=WHITE)
add_rect(s, 6.4, 3.9, 6.7, 0.4, fill=PURPLE)
add_text(s, "CI/CD Pipeline", 6.55, 3.93, 6.4, 0.35, size=12, bold=True, color=WHITE)
add_text_block(s, [
    "CI (ci.yml): PR → build + unit tests + tf validate + tf fmt",
    "CD (cd.yml): merge to main → tf plan (PR comment)",
    "           → manual approval → tf apply → k8s deploy",
    "OIDC federated credential for GitHub Actions (no secrets)",
    "docker image: ghcr.io/org/rag-poc-api:<sha>",
], 6.55, 4.37, 6.4, 1.95, size=10.5, color=DGRAY)

# ══════════════════════════════════════════════════════════════════════════════
# SLIDE 13 — Enterprise Security
# ══════════════════════════════════════════════════════════════════════════════
s = prs.slides.add_slide(BLANK)
slide_bg(s)
title_bar(s, "Enterprise Security Architecture",
          "Zero-trust · Workload Identity · No secrets in code")

sec_cards = [
    (0.3, 1.3, 4.0, "Identity (Zero Secrets)",
     ["• Workload Identity ONLY (OIDC federated)",
      "• AKS: 2 MIs — control-plane + kubelet",
      "• Pod annotation → user-assigned MI client-id",
      "• GitHub Actions: OIDC federated (no PAT)",
      "• Terraform state: MSI auth on azurerm backend",
      "• NEVER: client_secret or connection strings in code"], RED),
    (4.6, 1.3, 4.0, "Secret Management",
     ["• All secrets in Azure Key Vault (Standard SKU)",
      "• RBAC mode — not legacy access policies",
      "• Mounted via CSI Secrets Store driver",
      "• Never via env vars or ConfigMaps",
      "• .env.local gitignored — Key Vault refs for local dev",
      "• Secret rotation: Key Vault handles versioning"], ORANGE),
    (8.9, 1.3, 4.1, "Network Security",
     ["• AKS: Azure CNI Overlay — no external pod IPs",
      "• Ingress: NGINX (private) — no direct pod access",
      "• Service Bus: private endpoint",
      "• AI Search: private endpoint (prod)",
      "• CosmosDB: private endpoint",
      "• Key Vault: network ACLs + private endpoint"], NAVY),
    (0.3, 4.1, 4.0, "Data Protection",
     ["• CosmosDB: /tenantId partition — data isolation",
      "• Loan data: encrypted at rest (Azure-managed keys)",
      "• PII fields: SSN stored as hash only",
      "• Audit log: all decisions in CosmosDB",
      "• HMDA/ECOA: fair lending compliance check per app",
      "• AML/KYC: checked via CompliancePlugin"], PURPLE),
    (4.6, 4.1, 4.0, "LLM Security",
     ["• Prompt injection guard in system prompts",
      "• Output validated: strict JSON schema",
      "• No user input injected raw into prompts",
      "• Agent outputs parsed + sanitised before use",
      "• Llama on private AKS — no external model calls",
      "• GPT-4o via private Azure OpenAI endpoint"], BLUE),
    (8.9, 4.1, 4.1, "Compliance",
     ["• ECOA: fair lending check per application",
      "• HMDA: reporting requirements validated",
      "• AML/KYC: screening on every applicant",
      "• Explainability: all agent scores logged",
      "• Audit trail: full decision path in CosmosDB",
      "• RAG: CFPB/ECOA docs in compliance index"], GREEN),
]
for l, t, w, heading, bullets, col in sec_cards:
    card(s, l, t, w, 2.55, heading, bullets, head_color=col)

# ══════════════════════════════════════════════════════════════════════════════
# SLIDE 14 — RAG Implementation Detail
# ══════════════════════════════════════════════════════════════════════════════
s = prs.slides.add_slide(BLANK)
slide_bg(s)
title_bar(s, "RAG Implementation — Code Architecture",
          "How ISemanticTextMemory integrates into each plugin")

add_rect(s, 0.3, 1.25, 8.2, 5.5, fill=WHITE)
add_rect(s, 0.3, 1.25, 8.2, 0.4, fill=NAVY)
add_text(s, "Plugin RAG Pattern (Semantic Kernel)", 0.45, 1.28, 8.0, 0.35,
         size=12, bold=True, color=WHITE)

code = [
    "// Plugin constructor — inject memory",
    "public sealed class CompliancePlugin(",
    "    ISemanticTextMemory memory,",
    "    ILogger<CompliancePlugin> logger)",
    "",
    "// RAG retrieval inside plugin method",
    "public async Task<string> RunKycCheckAsync(...)",
    "{",
    "    // 1. Build semantic query from applicant data",
    "    var query = $\"KYC requirements for {applicantName}\";",
    "",
    "    // 2. Retrieve relevant regulation chunks",
    "    var results = memory.SearchAsync(",
    "        collection: \"compliance-index\",",
    "        query: query,",
    "        limit: 5,",
    "        minRelevanceScore: 0.75);",
    "",
    "    var context = await results",
    "        .Select(r => r.Metadata.Text)",
    "        .AggregateAsync((a,b) => a + \"\\n\" + b);",
    "",
    "    // 3. Return enriched context to orchestrator",
    "    // → injected into agent system prompt",
    "    return JsonSerializer.Serialize(new {",
    "        kycContext: context, applicant: applicantName });",
    "}",
]
add_text_block(s, code, 0.45, 1.72, 7.9, 4.9,
               size=10, color=DGRAY)

add_rect(s, 8.8, 1.25, 4.5, 5.5, fill=WHITE)
add_rect(s, 8.8, 1.25, 4.5, 0.4, fill=PURPLE)
add_text(s, "DI Registration", 8.95, 1.28, 4.3, 0.35, size=12, bold=True, color=WHITE)
add_text_block(s, [
    "// KernelExtensions.cs",
    "services.AddSingleton<ISemanticTextMemory>(",
    "  sp => new MemoryBuilder()",
    "    .WithAzureOpenAITextEmbeddingGeneration(",
    "      \"text-embedding-3-large\",",
    "      endpoint, credential)",
    "    .WithMemoryStore(",
    "      new AzureAISearchMemoryStore(",
    "        searchEndpoint, credential))",
    "    .Build());",
    "",
    "// Index names per plugin:",
    "// compliance-index",
    "// credit-index",
    "// risk-index",
    "// document-index",
    "// officer-precedent-index",
    "",
    "// Each index: hybrid BM25 + vector",
    "// text-embedding-3-large (3072 dims)",
    "// Semantic reranker ON",
], 8.95, 1.72, 4.3, 4.9, size=9.5, color=DGRAY)

# ══════════════════════════════════════════════════════════════════════════════
# SLIDE 15 — Mind Map: Full System
# ══════════════════════════════════════════════════════════════════════════════
s = prs.slides.add_slide(BLANK)
slide_bg(s)
title_bar(s, "Full System Mind Map",
          "Quick reference — all concepts at a glance")

# center
add_rect(s, 5.4, 3.1, 2.5, 0.85, fill=NAVY)
add_text(s, "Loan Approval\nAgentic AI", 5.4, 3.1, 2.5, 0.85,
         size=12, bold=True, color=WHITE, align=PP_ALIGN.CENTER)

branches = [
    (0.3, 1.3, 2.8, 1.9, "Agents",
     ["• 5 specialist agents", "• Sequential pipeline", "• Fresh ChatHistory each", "• JSON output contract"], BLUE),
    (0.3, 3.4, 2.8, 1.9, "Models",
     ["• Llama 3.1 → agents 1-2", "• GPT-4o-mini → agents 3-4", "• GPT-4o → LoanOfficer", "• Ollama local dev"], PURPLE),
    (0.3, 5.5, 2.8, 1.5, "Cost",
     ["• Hybrid saves 60-70%", "• 10M/yr saves ~$237k", "• Cache saves +$15k/1M"], GREEN),
    (4.8, 1.2, 3.7, 1.2, "RAG",
     ["Per-plugin Azure AI Search index · hybrid BM25+vector · semantic reranker"]),
    (9.2, 1.3, 4.0, 1.9, "Azure Stack",
     ["• Azure OpenAI (GPT-4o)", "• AI Search (5 indexes)", "• AKS GPU + system pool", "• CosmosDB audit log"], ORANGE),
    (9.2, 3.4, 4.0, 1.9, "Security",
     ["• Workload Identity (OIDC)", "• Key Vault CSI mount", "• No secrets in code", "• ECOA/AML/KYC compliant"], RED),
    (9.2, 5.5, 4.0, 1.5, "Streaming",
     ["• GetStreamingChatMessageContentsAsync", "• SSE → client live tokens", "• 60% perceived latency drop"], NAVY),
    (4.8, 5.5, 3.7, 1.5, "Caching",
     ["L1 exact · L2 semantic · L3 RAG chunk · Redis · ~$15k saving/1M req"]),
]
for l, t, w, h, heading, *rest in branches:
    bullets = rest[0] if rest else []
    col = rest[1] if len(rest) > 1 else BLUE
    card(s, l, t, w, h, heading, bullets if isinstance(bullets, list) else [bullets], head_color=col)

# ══════════════════════════════════════════════════════════════════════════════
# SLIDE 16 — Roadmap
# ══════════════════════════════════════════════════════════════════════════════
s = prs.slides.add_slide(BLANK)
slide_bg(s)
title_bar(s, "Implementation Roadmap",
          "v1 → v2 (RAG + Hybrid) → v3 (Streaming + Scale)")

phases = [
    ("Phase 1 — DONE ✅", "v1: Working Multi-Agent Pipeline",
     ["✅ 5 specialist agents + LoanOfficer",
      "✅ Plugin pre-compute (math + rules)",
      "✅ Semantic Kernel integration",
      "✅ Ollama local dev (llama3.1)",
      "✅ JSON contract per agent",
      "✅ Azure OpenAI ready (config-driven)"], GREEN, 0.3),
    ("Phase 2 — Next", "v2: RAG + Hybrid Models",
     ["• Deploy Llama on AKS GPU node pool",
      "• Wire ISemanticTextMemory per plugin",
      "• 5 Azure AI Search indexes (one per domain)",
      "• Ingest: regulations, credit bureau, rate data",
      "• GPT-4o-mini for agents 3-4",
      "• Semantic cache (Redis L2)"], BLUE, 4.6),
    ("Phase 3 — Future", "v3: Streaming + Scale",
     ["• GetStreamingChatMessageContentsAsync",
      "• SSE endpoint + SignalR hub",
      "• Service Bus priority queue (async batch)",
      "• APIM rate limiting + auth",
      "• Multi-tenant (CosmosDB /tenantId)",
      "• Grafana dashboards + alerts"], PURPLE, 8.9),
]
for title, subtitle, bullets, col, l in phases:
    add_rect(s, l, 1.25, 4.1, 5.25, fill=WHITE)
    add_rect(s, l, 1.25, 4.1, 0.55, fill=col)
    add_text(s, title, l+0.1, 1.27, 3.9, 0.3, size=12, bold=True, color=WHITE)
    add_text(s, subtitle, l+0.1, 1.57, 3.9, 0.22, size=10, color=RGBColor(0xCC,0xEE,0xFF), italic=True)
    add_text_block(s, bullets, l+0.15, 1.88, 3.8, 4.5, size=11, color=DGRAY)

add_rect(s, 0.3, 6.7, 12.8, 0.55, fill=NAVY)
add_text(s, "Phase 2 estimated effort: 3-4 sprints  ·  "
            "Phase 3 estimated effort: 2-3 sprints  ·  "
            "No breaking changes to agent contract",
         0.5, 6.75, 12.5, 0.45, size=11, color=CYAN)

# ══════════════════════════════════════════════════════════════════════════════
# SLIDE 17 — Interview / Client Quick Reference
# ══════════════════════════════════════════════════════════════════════════════
s = prs.slides.add_slide(BLANK)
slide_bg(s)
title_bar(s, "Quick Reference — Interviewer / Client Talking Points",
          "One-liner answers to the most common questions")

qa = [
    ("What problem does this solve?",
     "Automates multi-step loan underwriting using AI agents — each specialist role mapped to an agent with a specific knowledge domain."),
    ("Why multiple agents instead of one?",
     "Separation of concerns: each agent has a narrow prompt → less hallucination, easier to audit, independent model sizing per role."),
    ("Why Llama for some agents and GPT-4o for LoanOfficer?",
     "Cost + accuracy tradeoff: extraction & scoring = Llama (cheap, fast). Final decision = GPT-4o (highest accuracy, legally defensible)."),
    ("How does RAG improve this?",
     "Plugins currently have hardcoded rules. RAG replaces those with real-time retrieval from regulatory docs, rate tables, and precedent decisions."),
    ("What's the cost saving?",
     "Hybrid model routing saves ~60-70% vs all-GPT-4o. At 10M requests/yr that's ~$237,000 annual saving."),
    ("How is it enterprise-secure?",
     "Workload Identity (no secrets), Key Vault CSI, private endpoints, ECOA/AML/KYC compliance checks, full audit log in CosmosDB."),
    ("Can it scale to high volume?",
     "Yes: AKS HPA per agent, Service Bus async queue, semantic cache (40%+ LLM call reduction), spot GPU nodes for Llama agents."),
    ("What's the streaming plan?",
     "Replace blocking GetChatMessageContentAsync with GetStreamingChatMessageContentsAsync + SSE. Perceived latency drops ~60%."),
]
for i, (q, a) in enumerate(qa):
    row = i // 2
    col_offset = i % 2
    l = 0.3 + col_offset * 6.5
    t = 1.3 + row * 1.35
    add_rect(s, l, t, 6.2, 1.25, fill=WHITE)
    add_rect(s, l, t, 6.2, 0.32, fill=NAVY)
    add_text(s, f"Q: {q}", l+0.1, t+0.03, 6.0, 0.28, size=10, bold=True, color=CYAN)
    add_text(s, a, l+0.12, t+0.36, 5.95, 0.85, size=10, color=DGRAY, wrap=True)

# ══════════════════════════════════════════════════════════════════════════════
# SLIDE 18 — Observability & Monitoring
# ══════════════════════════════════════════════════════════════════════════════
s = prs.slides.add_slide(BLANK)
slide_bg(s)
title_bar(s, "Observability & Monitoring",
          "What to watch in production — logs, metrics, alerts")

obs_cards = [
    (0.3, 1.3, 4.0, "Structured Logging",
     ["• ILogger<T> on every agent call",
      "• Log: agent name, elapsed time, char count",
      "• Log: passed/score/concerns per report",
      "• Log: plugin pre-compute metrics",
      "• Warning: empty agent response",
      "• Error: exception per agent (non-fatal)"], BLUE),
    (4.6, 1.3, 4.0, "Key Metrics to Track",
     ["• Agent p50/p95/p99 latency",
      "• LoanOfficer timeout rate (was 503 issue)",
      "• Cache hit rate (L1 + L2 + L3)",
      "• Token usage per agent (Azure OpenAI)",
      "• GPU utilisation on Llama nodes",
      "• JSON parse failure rate per agent"], ORANGE),
    (8.9, 1.3, 4.1, "Azure Monitor Setup",
     ["• App Insights SDK in ASP.NET Core",
      "• Custom dimensions: applicationId, agentName",
      "• Azure Monitor Workbook: agent pipeline view",
      "• Alert: >5% agent failure rate",
      "• Alert: LoanOfficer latency >60s",
      "• Dashboard: grafana.internal/d/api-latency"], NAVY),
    (0.3, 4.1, 4.0, "Known Issues (from debug log)",
     ["• 503 on LoanOfficer: increase TimeoutMinutes",
      "• Empty response: add retry with backoff",
      "• 100% CPU: check GPU — use llama3.2:1b fallback",
      "• JSON parse fail: llama adds markdown fences",
      "  → handled by TryParseAgentReport() regex",
      "• Ollama cold start: set min-replicas=1 on AKS"], RED),
    (4.6, 4.1, 8.4, "SLA Targets (production)",
     ["• P99 end-to-end < 45 seconds (GPU-accelerated Llama + Azure OpenAI)",
      "• LoanOfficer < 15 seconds (GPT-4o, ~2K tokens)",
      "• Cache hit reduces p50 to < 200ms (Redis exact match)",
      "• Availability: 99.9% (AKS multi-zone + Azure OpenAI SLA)",
      "• Throughput: 500 concurrent applications (HPA max=10 per agent)"], GREEN),
]
for l, t, w, heading, bullets, col in obs_cards:
    card(s, l, t, w, 2.55, heading, bullets, head_color=col)

# ══════════════════════════════════════════════════════════════════════════════
# SLIDE 19 — Decision Flow Diagram
# ══════════════════════════════════════════════════════════════════════════════
s = prs.slides.add_slide(BLANK)
slide_bg(s)
title_bar(s, "LoanOfficer Decision Logic Flow",
          "Hard denial rules → weighted scoring → final verdict")

# Flow chart using boxes and arrows
steps = [
    (5.1, 1.3, 3.1, 0.55, "Receive 4 Agent Reports", NAVY),
    (5.1, 2.15, 3.1, 0.55, "KYC Failed? AML Failed?", RED),
    (1.5, 2.9, 2.8, 0.55,  "→ HARD REJECT", RED),
    (5.1, 2.9, 3.1, 0.55, "Credit Score < 500?", RED),
    (1.5, 3.65, 2.8, 0.55, "→ HARD REJECT", RED),
    (5.1, 3.65, 3.1, 0.55, "DTI > 50%?", RED),
    (1.5, 4.4, 2.8, 0.55,  "→ HARD REJECT", RED),
    (5.1, 4.4, 3.1, 0.55,  "Weighted Score\n(Doc 20 · Credit 35 · Risk 30 · Compliance 15)", PURPLE),
    (5.1, 5.35, 3.1, 0.55, "Score ≥75 + Low/Medium Risk?", BLUE),
    (9.5, 5.1,  2.8, 0.55, "→ APPROVED ✅", GREEN),
    (5.1, 6.1,  3.1, 0.55, "Score ≥60?", BLUE),
    (9.5, 5.85, 2.8, 0.55, "→ CONDITIONAL ⚠️", ORANGE),
    (9.5, 6.6,  2.8, 0.55, "→ REJECTED ❌", RED),
]
for l, t, w, h, label, col in steps:
    add_rect(s, l, t, w, h, fill=col)
    add_text(s, label, l+0.05, t+0.04, w-0.1, h-0.08, size=10, bold=True,
             color=WHITE, align=PP_ALIGN.CENTER)

add_rect(s, 0.3, 1.25, 4.5, 5.6, fill=WHITE)
add_rect(s, 0.3, 1.25, 4.5, 0.38, fill=NAVY)
add_text(s, "Weighting Rationale", 0.45, 1.28, 4.2, 0.33, size=11, bold=True, color=WHITE)
add_text_block(s, [
    "Credit (35%) — most predictive of default",
    "Risk (30%) — LTV, employment, composite score",
    "Compliance (15%) — regulatory gate (binary)",
    "Documents (20%) — data quality confidence",
    "",
    "Hard Denial Triggers (any one = reject):",
    "• KYC failure → identity risk",
    "• AML failure → regulatory mandatory",
    "• Credit < 500 → below lender floor",
    "• DTI > 50% → unsustainable debt load",
    "",
    "Scoring:",
    "≥75 + Low/Medium risk → Approved",
    "≥60 → ConditionallyApproved",
    "< 60 → Rejected",
    "No LoanOfficer report → PendingManualReview",
], 0.45, 1.7, 4.3, 5.0, size=10, color=DGRAY)

# ══════════════════════════════════════════════════════════════════════════════
# SLIDE 20 — Summary
# ══════════════════════════════════════════════════════════════════════════════
s = prs.slides.add_slide(BLANK)
add_rect(s, 0, 0, 13.33, 7.5, fill=NAVY)
add_rect(s, 0, 1.8, 13.33, 0.08, fill=BLUE)
add_rect(s, 0, 5.6, 13.33, 0.08, fill=BLUE)

add_text(s, "Summary — What We've Built & Where We're Going",
         0.5, 0.25, 12.3, 0.7, size=22, bold=True, color=WHITE, align=PP_ALIGN.CENTER)
add_text(s, "Loan Approval Agentic AI · Enterprise Production Roadmap",
         0.5, 0.95, 12.3, 0.4, size=13, color=CYAN, align=PP_ALIGN.CENTER, italic=True)

summary_items = [
    ("✅ Built", "5-agent sequential pipeline · Semantic Kernel · Plugin pre-compute · Ollama local dev", GREEN),
    ("🚀 Next", "RAG per plugin (5 AI Search indexes) · Llama on AKS · Hybrid model routing", CYAN),
    ("💰 Saving", "60-70% cost reduction via hybrid models · ~$237k/yr at 10M requests", ORANGE),
    ("🔒 Secure", "Workload Identity · Key Vault CSI · ECOA/AML/KYC · No secrets in code", RGBColor(0xFF,0xCC,0x00)),
    ("📡 Future", "Streaming (SSE) · Semantic cache · APIM · Multi-tenant · Grafana dashboards", PURPLE),
]
for i, (tag, desc, col) in enumerate(summary_items):
    y = 2.1 + i * 0.65
    add_rect(s, 0.5, y, 1.3, 0.5, fill=col)
    add_text(s, tag, 0.5, y, 1.3, 0.5, size=12, bold=True, color=NAVY, align=PP_ALIGN.CENTER)
    add_text(s, desc, 2.1, y+0.08, 10.8, 0.4, size=12, color=WHITE)

add_text(s, "Stack: .NET 8 · ASP.NET Core · Semantic Kernel · Azure OpenAI · Azure AI Search · AKS · Ollama · Redis · Terraform",
         0.5, 5.8, 12.3, 0.4, size=10, color=RGBColor(0xAA,0xBB,0xCC),
         align=PP_ALIGN.CENTER, italic=True)
add_text(s, "github: LoanApprovalAgenticAI  ·  Abhishek Sinha",
         0.5, 6.5, 12.3, 0.4, size=11, color=CYAN,
         align=PP_ALIGN.CENTER)

# ══════════════════════════════════════════════════════════════════════════════
# SAVE
# ══════════════════════════════════════════════════════════════════════════════
out = r"C:\aiPrj\LoanApprovalAgenticAI\Presentation\LoanApprovalAgenticAI_Presentation.pptx"
prs.save(out)
print(f"Saved: {out}  ({len(prs.slides)} slides)")
