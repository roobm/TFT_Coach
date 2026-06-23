# Feature: Match-History AI Analysis (RAG + Structured + Trends)

## Goal
Upgrade the existing `AnalyzeMatchHistory` use case from a single free-text blob into an
in-depth, structured analysis that is:
1. **Structured** — typed fields (avg placement, top-4 rate, win rate, top comps, strengths,
   weaknesses, suggestions, narrative) instead of one text string.
2. **RAG-grounded** — retrieves relevant patch/meta context from Supabase `tft_patch_data`
   (pgvector) and feeds it to LM Studio so advice reflects the current patch.
3. **Trend-aware + persisted** — stores fetched matches in Supabase and computes placement
   trends over time.

Match-history analysis is **in-depth** (the 2-sentence/[TAG] limit applies only to live
coaching, not here). Inference stays local (LM Studio only).

## Affected Layers
- **Domain**: `MatchAnalysis`, `CompPerformance`, `PlacementTrend`; extend `IVectorRepository`
  with similarity search; new `IMatchHistoryRepository`.
- **Application**: rewrite `AnalyzeMatchHistoryUseCase`; new structured `Response`; `Request`
  gains optional `Puuid`.
- **Infrastructure**: `SupabaseVectorRepository.SearchAsync`; new `SupabaseMatchHistoryRepository`.
- **Supabase**: migration to let `match_history` store matches keyed by `puuid` + `match_id`.
- **API**: DI wiring for new repo; pass `puuid` through `/matches/analysis`.
- **Frontend**: typed `AnalysisResponse` + structured render in the analysis UI.
- **Tests**: update `AnalyzeMatchHistoryUseCaseTests` + add coverage for stats/trends/RAG.

## Ordered Task List
- [x] 0. Create branch `feature/match-history-ai-analysis` from `main`.
- [x] 1. Domain: analysis entities + `IVectorRepository.SearchAsync` + `IMatchHistoryRepository`.
- [x] 2. Application: deterministic stats + trend calc + RAG retrieval + structured LLM output;
       new `Response`, `Request.Puuid`.
- [x] 3. Infrastructure: vector cosine search + match-history persistence repository.
- [x] 4. Supabase migration: add `puuid` + `match_id` (unique) to `match_history`.
- [x] 5. API: register repos in DI; thread `puuid` into the analysis endpoint.
- [x] 6. Frontend: structured analysis types + UI.
- [x] 7. Tests: xUnit AAA coverage for the use case.
- [ ] 8. `dotnet build` + `dotnet test`; apply migration; fix failures.

## Notes / Decisions
- Numeric stats are computed deterministically in code (never asked of the LLM) to avoid
  hallucinated numbers. The LLM only writes narrative/strengths/weaknesses/suggestions, grounded
  in the computed stats + retrieved patch context, and must return JSON.
- A match's "comp" label = its top 1–2 active traits (Traits are already sorted highest-first).
- Trend = compare average placement of the older half vs the newer half of the request window
  (lower placement = better → improving). Persistence is a bonus for future cross-session trends.
- Persistence is keyed by `puuid` (no auth yet); `user_id` stays nullable. Save is best-effort:
  a persistence failure must not fail the analysis response.
