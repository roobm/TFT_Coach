# Feature: Ground match-history AI analysis on live meta

## Goal
Make the match-history analysis coach (`AnalyzeMatchHistoryUseCase`) compare the
player's most-played comps (`topComps`) against the current meta comps (from
`IMetaProvider`) and explicitly surface the closest fit / whether they're playing
meta. Mirror the live coach's meta-grounding pattern without changing the live
coach behaviour.

## Affected layers
- **Application** — `AnalyzeMatchHistoryUseCase` (inject `IMetaProvider`, fetch meta
  best-effort, add a meta + meta-fit prompt section, minimal `SystemPrompt` tweak).
- **Tests** — `AnalyzeMatchHistoryUseCaseTests` (fix ctor, add meta cases).
- Domain untouched; Infrastructure untouched; API DI already registers `IMetaProvider`.

## Task list
1. [x] Branch + plan file.
2. [x] Inject `IMetaProvider` into `AnalyzeMatchHistoryUseCase` ctor.
3. [x] Fetch meta best-effort (try/catch + structured logging) in `ExecuteAsync`.
4. [x] Add `AppendMetaSection` + meta-fit mapping in `BuildPrompt` (cap at 6 comps,
   case-insensitive overlap of trait labels/units/traits vs meta Name/Carries).
5. [x] Minimal `SystemPrompt` update describing the new meta + meta-fit data.
6. [x] Fix existing tests for new ctor; add new meta tests.
7. [x] `dotnet build` + `dotnet test` green.

## Notes
- `IMetaProvider` registered as singleton in `Program.cs` (live coach) — no new DI needed.
- Best-effort: null/failed meta means analysis proceeds with no meta section.
- Do NOT touch `GetSuggestionUseCase`.
