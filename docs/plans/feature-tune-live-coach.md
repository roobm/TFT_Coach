# Feature: Tune & Harden the Live Coach

## Goal
Sharpen the real-time, in-game suggestion feature for a polished demo: timing-aware
economy/level advice, board→meta fitting, rock-solid tag/format compliance, and
deterministic output guardrails — all while keeping the non-negotiable output
contract intact (local LM Studio only, stateless, one tagged line, ≤2 sentences,
action-verb first, `/no_think` suffix retained).

## Affected layers
- **Application** (primary): `UseCases/GetSuggestion/GetSuggestionUseCase.cs`
  - `SystemPrompt` rewrite (timing heuristics, comp-fitting, tag discipline, few-shot).
  - `BuildPrompt` grounding: board→meta overlap detection from richer decoded units.
  - `ParseSuggestion` guardrails: collapse newlines to one line, trim to ≤2 sentences.
- **Application.Tests**: extend `GetSuggestionUseCaseTests` for the new guarantees.
- **Domain / Infrastructure / API**: NOT touched.

## Ordered task list
1. [x] Read existing use case, prompt, parsing, request/response, GameState, meta, tests.
2. [x] Create this plan file.
3. [x] Rewrite `SystemPrompt`: timing heuristics + comp-fitting + tag selection + per-tag few-shot.
4. [x] Add board→meta fit grounding in `BuildPrompt` (overlap of current units vs meta carries).
5. [x] Add deterministic guardrails in `ParseSuggestion` (single-line collapse + ≤2-sentence trim).
6. [x] Extend tests: one-line normalization, ≤2-sentence trim, action-verb preserved,
   tag parsing intact, meta grounding intact, board-fit section present, system prompt few-shot present.
7. [ ] `dotnet build TFTCoach.slnx` + `dotnet test TFTCoach.slnx` — both green. (run by user)

## Notes / assumptions
- Unit matching is case-insensitive substring so decoded labels like `Aphelios 2★`
  still match a meta carry named `Aphelios`.
- Guardrails only normalize/trim what the model returned — no fabricated content.
- System-prompt few-shot lines and heuristics need live-model verification for tone/accuracy.
