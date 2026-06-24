# Feature: Season selection + History page

## Goal
Let users browse match history by **TFT set (season)** and view a dedicated
**History page** with the rank-over-time chart.

User asks:
1. Rename the "All games" count option → "All (this season)".
2. Add a **season dropdown** to pick which set's matches to view; older seasons
   are **fetched on demand** from Riot (rate-limited, slow first time).
3. Add a **separate History page** (real route) showing the **rank history chart**.

## Decisions (from user)
- Routing: **react-router** with a real `/history` route.
- Seasons: **fetch older seasons on demand** (live from Riot).
- History page scope: **rank history chart only** (recorded snapshots).

## Affected layers
- **Application**: `GetMatchHistoryRequest.Set` (optional target set); use case
  generalized to target an arbitrary set; `GetMatchHistoryResponse.CurrentSet`.
- **API**: `GET /matches` accepts `set` query param; response carries currentSet.
- **Frontend**: react-router; `MatchesPage` (existing UI + season dropdown +
  rename); `HistoryPage` (rank chart); nav; `fetchMatches(set?)` + types.
- **Tests**: use-case tests for targeting a specific older set.

## Backend design — targeting a set
Match ids are newest-first; set is only known after fetching detail. To return a
target set we walk ids newest→oldest:
- `targetSet = request.Set > 0 ? request.Set : currentSet` (currentSet = newest
  match's set, re-fetched once if cached value is unknown).
- For each match, `effectiveSet = SetNumber == 0 ? currentSet : SetNumber`
  (legacy unknown rows are current-season).
- `effectiveSet > targetSet` → skip (newer than target).
- `effectiveSet < targetSet` → break (older than target; stop).
- `effectiveSet == targetSet` → include (respect `count`).

Fetching an older set therefore pulls detail for all newer sets too (cached after
first time) — the accepted cost of on-demand history.

## Ordered task list
1. [x] Branch `feature/history-page` + plan file.
2. [x] Application: add `Set` to request, `CurrentSet` to response, generalize
   use case to target a set.
3. [x] API: accept `set` query param on `GET /matches`.
4. [x] Tests: target older set / skip newer / stop at older.
5. [x] Frontend: add `react-router-dom`, split `MatchesPage` / `HistoryPage`,
   nav header, route setup in `main.tsx` / `App.tsx`.
6. [x] Matches page: season `<select>` (current + previous sets), rename "All
   games" → "All this season", wire `fetchMatchHistory(set)`.
7. [x] History page: fetch ranks, render `RankClimbChart` from recorded history.
8. [x] `dotnet build` + `dotnet test`; `npm run build`; verify in browser.

## Status
- Complete. Verified in browser: Matches page season dropdown populates
  "This season (Set 17)" → "Set 11" after search, results header shows
  "· Set 17"; `/history` route renders its own form + `RankClimbChart`
  ("▲ 320 LP est. over 218 ranked"). `dotnet test` 35 pass; `npm run build` OK.

## Notes / caveats
- Selecting an **older** season is slow the first time (Riot rate limiter must
  page through all newer sets to discover each match's set).
- History-page rank chart needs ≥2 ranked matches or recorded snapshots to draw
  a line; sparse accounts show only the current point.
- Uses `BrowserRouter`; deep-linking `/history` relies on SPA fallback (Vite dev
  handles this — production host must rewrite to `index.html`).
