---
name: refactor-workflow
description: Plans and executes safe refactors in this repo. Use when the user asks to refactor, restructure, rename, or clean up code while preserving behavior. Starts by finding usage sites with semantic search, proposes stepwise changes and risks, can use Ollama MCP for first-pass bulk edits, then validates with compile and tests.
---

# Refactor Workflow

Use this skill for behavior-preserving refactors.

## Quick Start

1. Discover current usage sites with semantic search before editing.
2. Propose a short refactor plan and explicit risk list.
3. Optionally use Ollama MCP to draft bulk edits.
4. Validate manually, fix compile issues, and run tests.

## Required Workflow

### 1) Discover usage sites first

- Run semantic search to find:
  - call sites
  - interface/implementation boundaries
  - config/DI wiring
  - tests covering the target area
- Also run exact-text search for symbol names to catch misses.
- Summarize findings as "what exists now" before proposing changes.

### 2) Propose refactor steps + risk

Before making edits, provide:

- **Refactor steps**: smallest safe sequence (prefer incremental commits)
- **Behavioral risks**: what might break and why
- **Blast radius**: which projects/files are likely affected
- **Validation plan**: what build/tests will prove safety

Keep this concise and repo-specific.

### 3) Draft bulk edits with Ollama (optional)

Use Ollama MCP only for first-pass mechanical edits when helpful:

- renames across many files
- repetitive extraction/move operations
- boilerplate updates in tests

Treat Ollama output as a draft:

- verify against current repo patterns
- correct APIs/usages that do not match local conventions
- never trust generated edits without review

If Ollama MCP is unavailable, perform manual edits directly.

### 4) Validate and fix

After edits:

1. Run build/tests and fix compilation/test failures.
2. Re-check touched code paths for behavior parity.
3. Update or add tests where coverage is missing.

For this repo's .NET code:

- Prefer `dotnet test` as the primary gate.
- If tests are unavailable/slow, at minimum run `dotnet build`.

## Output Format

When reporting back, use this structure:

1. **Usage sites found** (key files/symbols)
2. **Refactor plan** (step-by-step)
3. **Risks** (ordered high → low)
4. **Edits made** (what changed)
5. **Validation results** (`dotnet test` / `dotnet build`)
6. **Follow-ups** (if any)

## Guardrails

- Preserve behavior unless the user asked for functional change.
- Avoid adding new packages unless explicitly requested.
- Do not swallow exceptions; preserve logging/error semantics.
- Prefer small, reviewable changes over large rewrites.
