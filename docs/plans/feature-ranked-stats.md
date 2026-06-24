# Feature: Ranked stats (Riot League-V1) + frontend rank display

## Goal
Expand Riot API usage beyond match history by surfacing the player's **ranked
standing** (tier, division, LP, wins/losses, win rate, hot streak) for their TFT
queues. Display it as a polished player summary card in the frontend so a user
immediately sees their rank alongside recent matches.

## Why / data-driven rationale
Match history already gives placements; rank adds the "where do I stand" context
(LP momentum, hot streak, queue split). It is a cheap single Riot call per
player and pairs naturally with the existing search flow.

## Routing note
- Account-V1 (PUUID resolution) and TFT-Match-V1 are **regional** (`europe`).
- **TFT-League-V1 is platform-routed** (`euw1`, `na1`, `kr`, ...). This feature
  adds a `RIOT_PLATFORM` env var (default `euw1`) and a separate platform-routed
  HttpClient. Reuses `IRiotMatchClient.ResolvePuuidAsync` for account lookup.

## Affected layers
- **Domain**: `RankedEntry` entity.
- **Application**: `IRiotLeagueClient` interface; `GetRanks` use case (3-file).
- **Infrastructure**: `RiotLeagueClient` (platform-routed) implementation.
- **API**: `RIOT_PLATFORM` config, league HttpClient + DI, `GET /ranks` endpoint.
- **Frontend**: `fetchRanks` + types, `RankCard` component, App integration,
  styling polish.
- **Tests**: `GetRanksUseCaseTests` (xUnit, AAA).

## Ordered task list
1. [x] Domain: add `RankedEntry` entity (queue, tier, division, LP, W/L, hot
   streak, computed win rate).
2. [x] Application: add `IRiotLeagueClient` interface.
3. [x] Application: add `GetRanks` use case (`GetRanksUseCase` / `Request` /
   `Response`).
4. [x] Infrastructure: implement `RiotLeagueClient` against TFT-League-V1.
5. [x] API: add `RIOT_PLATFORM`, platform HttpClient, DI registration, and
   `GET /ranks?riotId=...` endpoint. Update `.env` / `.env.example`.
6. [x] Frontend: `RankedEntry`/`RanksResponse` types + `fetchRanks`, `RankCard`
   component, App integration (parallel fetch), CSS polish.
7. [x] Tests: `GetRanksUseCaseTests`.
8. [x] `dotnet build` + `dotnet test`; fix failures.

## Status
- Complete. Backend builds, 31 tests pass, frontend type-checks clean.

## Extension: rank-climb graph (estimate + recorded snapshots)
Riot exposes no historical LP, so the climb graph combines two sources:
- **Estimated** absolute-LP curve derived from Ranked-TFT match placements
  (queue 1100), anchored to the player's current rank and walked backward.
- **Recorded** snapshots: each `/ranks` call captures the current standing into
  a new `rank_history` table (de-duplicated vs. the latest), building accurate
  history over time.

Added layers:
- Domain: `RankSnapshot`, `IRankHistoryRepository`, `TftMatchResult.QueueId`.
- Infrastructure: `SupabaseRankHistoryRepository`, queue_id persistence, Riot
  `queue_id` mapping. Migration `20260624120000_rank_history_and_queue_id.sql`.
- Application: `GetRanksUseCase` records snapshots + returns `History`.
- Frontend: `RankClimbChart` (SVG, tier bands, estimate + snapshot lines),
  `queueId`/`RankSnapshot` types, App integration, CSS.
- Tests: 2 added (`GetRanksUseCaseTests`) → 27 Application tests pass.

## Extension: current-set scoping for match history
A `count<=0` "All games" fetch previously pulled **every season** (~1000 matches
across 2025–2026), returning wrong counts and saturating the Riot rate limiter
for ~20 min. Match history is now scoped to the **current TFT set**.

- TFT set number (`info.tft_set_number`) is distinct from game patch — sets
  change mid-patch, so the set is read from match detail, not derived.
- Domain: `TftMatchResult.SetNumber`. Migration
  `20260624130000_match_history_set_number.sql` adds `match_history.set_number`.
  Insert switched to UPSERT to backfill `set_number`/`queue_id` on null.
- `GetMatchHistoryUseCase` walks Riot ids newest-first and **stops at the first
  match from an older set** (current set = newest match's set, re-fetched once if
  the cached value is unknown). Old seasons are never re-fetched.
- One-time cleanup: the polluted multi-season cache was cleared and re-synced,
  rebuilding 219 current-set (set 17) matches.
- Tests: 1 added (`ShouldStopAtPreviousSet_WhenMatchesSpanMultipleSets`) → 28
  Application tests pass.
- Future: a dedicated history page can expose older sets on demand.


