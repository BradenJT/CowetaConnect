# ADR-002: ML Framework Selection

**Date:** 2026-02-18  
**Status:** Accepted  
**Deciders:** Architecture Team

## Context

We need to train and run ML models for geographic demand signal scoring. The core question is whether to use ML.NET (C#) or introduce a Python ML ecosystem (scikit-learn, PyTorch, etc.).

## Decision

**ML.NET** for the initial lead scoring model.

## Rationale

- Keeps the entire stack in C# — no Python service, no IPC complexity, no separate deployment unit
- ML.NET supports binary classification with FastTree (gradient boosted trees) — appropriate for our tabular demand data
- Models are serializable to `.zip` files, easy to store in Azure Blob and load at runtime
- Microsoft maintains ML.NET actively with LTS guarantees
- Our dataset size (hundreds to low thousands of training rows initially) is well within ML.NET's capabilities
- Inference runs in-process with `PredictionEngine<>` — sub-millisecond latency

## Alternatives Considered

| Alternative | Reason Rejected |
|---|---|
| Python scikit-learn via subprocess | Cross-process call complexity, deployment friction |
| Azure ML Service | Overkill for dataset size; adds cost and operational complexity |
| ONNX Runtime (import Python models) | Still requires Python training environment; ONNX export adds friction |
| OpenAI API / LLM | Expensive, non-deterministic, not appropriate for structured tabular scoring |

## Consequences

- If we need deep learning or NLP in Phase 4+, we may revisit and introduce ONNX Runtime or a separate Python scoring service. The current architecture accommodates this by isolating ML behind the `ILeadScoringService` interface.
- ML.NET does not support GPU training — acceptable for our tabular data workload.
- Weekly retraining runs as a Hangfire background job inside the existing App Service.
