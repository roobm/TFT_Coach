# Feature: Live Coach page

## Goal
A dedicated **`/live-coach`** page where the player enters their current in-game
situation and gets a single, real-time coaching suggestion over **SignalR**.

## Decisions (from user)
- **Input:** structured form (stage, gold, level, health, streak, board units,
  items/bench) **plus** an optional free-text notes box.
- **Transport:** **SignalR hub** (`/hubs/coach`) — invoke/response pattern.
- **Grounding:** **No RAG for now** — pure board-state coaching, kept fast.

## Coaching output rules (enforced)
- Exactly one leading tag: `[ECONOMY] [BOARD] [ITEMS] [COMP] [SCOUT]`.
- Max 2 sentences; first starts with an action verb (Sell, Buy, Roll, Hold,
  Move, Build, Pivot, Scout).
- **Stateless** — no conversational history; each call is independent.

## Affected layers
- **Domain:** `GameState` (the board snapshot), `CoachingSuggestion`
  (category + advice text), `SuggestionCategory` enum
  (Economy | Board | Items | Comp | Scout).
- **Application:** `UseCases/GetSuggestion/` 3-file pattern. Builds the prompt
  from `GameState`, calls `ILMStudioClient.CompleteAsync`, parses the leading
  tag → `SuggestionCategory` (defaults to Board if missing), returns the advice.
- **API:** `Hubs/CoachHub.cs` with `GetSuggestion(request)` invoke method;
  `AddSignalR()`; `MapHub<CoachHub>("/hubs/coach")`; CORS `AllowCredentials`.
- **Frontend:** `@microsoft/signalr`; `coachHub.ts` connection helper;
  `LiveCoachPage.tsx` (form + suggestion card); nav link + route; CSS.
- **Tests:** `GetSuggestionUseCaseTests` — tag→category parsing, default
  fallback, prompt is built from state (mocked `ILMStudioClient`).

## Design notes
- The model returns one line like `[BOARD] Move Aphelios to the backline ...`.
  The use case strips/parses the tag, maps it to `SuggestionCategory`, and
  returns `{ Category, Text }`. Frontend renders a tag pill + the advice.
- Qwen3 soft-switch: append `/no_think` to the user turn (same as analysis).
- Hub uses the invoke return pattern so the client `await`s the suggestion;
  no server-push needed for this manual-input version.

## Ordered task list
1. [x] Branch `feature/live-coach` + plan file.
2. [x] Domain: `GameState`, `CoachingSuggestion`, `SuggestionCategory`.
3. [x] Application: `GetSuggestion` use case (request/response/use case).
4. [x] Tests: tag→category parsing + default fallback.
5. [x] API: `CoachHub`, `AddSignalR`, `MapHub`, CORS `AllowCredentials`, DI.
6. [x] Frontend: `@microsoft/signalr`, `coachHub.ts`, `LiveCoachPage`, route +
   nav link, styles.
7. [x] `dotnet build` + `dotnet test`; `npm run build`; verify in browser.

## Status
- Complete. Verified end-to-end in the browser: the `/live-coach` form connects
  over SignalR and returns a tagged suggestion, e.g. `[ECONOMY] Sell a component
  to roll…` rendered with the gold tag pill. `dotnet test` 44 pass; `npm run
  build` OK.

## Gotchas found
- SignalR counts a `CancellationToken` parameter as a client argument ("provides
  1 argument(s) but target expects 2"). The hub method takes only the request and
  uses `Context.ConnectionAborted` for cancellation.
- The SignalR JSON protocol is separate from `ConfigureHttpJsonOptions`, so the
  `JsonStringEnumConverter` is registered via `AddJsonProtocol` too (otherwise
  `SuggestionCategory` serialises as an integer).
- CORS needs `AllowCredentials()` for the browser SignalR negotiate/websocket.
- `dotnet test` does **not** rebuild the API project (tests don't reference it),
  so after changing the hub run `dotnet build src/TFTCoach.API` before
  `dotnet run --no-build` — otherwise the client hits "Method does not exist".
- A shared lazy `HubConnection` must serialise concurrent starts (React
  StrictMode double-invokes the effect): track an in-flight `startPromise` and
  await it so callers never `invoke` while the socket is still `Connecting`.

---

## Enhancement: Coach Online toggle + meta grounding

### Why this shape (constraints found)
- **No official TFT live-game API.** Riot's Spectator API and the local Live
  Client Data API (`127.0.0.1:2999`) are **League/Summoner's-Rift only** — they
  expose nothing for TFT (no board, gold, units). True auto-detection of an
  active TFT game isn't possible via supported APIs. So "in a game" is a
  **manual toggle**.
- **No documented public meta API** (MetaTFT/tactics.tools/TFT Academy). Meta is
  a **curated, patch-tagged local file** behind `IMetaProvider`, so a community
  feed can be swapped in later without touching the use case.

### What was added
- **Domain:** `MetaComp` (name, tier, carries, key items, playstyle, notes) and
  `MetaSnapshot` (set, patch, comps).
- **Application:** `IMetaProvider.GetCurrentMetaAsync()`. `GetSuggestionUseCase`
  now takes `IMetaProvider`, renders a compact, patch-tagged meta section into
  the prompt (top 6 comps), and a system-prompt rule to prefer a strong comp
  when it fits. Returns `null` meta gracefully (first-principles advice).
- **Infrastructure:** `FileMetaProvider` reads + caches a curated JSON file;
  missing/malformed file logs and returns `null` (non-fatal).
- **API:** curated `data/meta/current.json` (copied to output, `META_FILE_PATH`
  override); `IMetaProvider` registered as a singleton; `CoachHub.GetMeta()`
  returns `{ set, patch, comps[] }` for the badge.
- **Frontend:** `coachHub.ts` gains `connect/disconnect/getMeta` and a
  concurrency-safe connection. `LiveCoachPage` has a **Coach online** switch
  (persisted to `localStorage`) that opens/closes the connection and loads the
  meta; a **Live/Idle** status pill; a **meta badge** (`Set · patch`, comps in
  the tooltip); and the form is disabled (`<fieldset disabled>`) when offline.
- **Tests:** meta-section-in-prompt and meta-omitted-when-null. 46 pass total.

### Verified
- Toggle off → form disabled, pill "Idle". Toggle on → pill "Live", badge
  "Meta: Set 14: Cyber City · patch 14.6". Submitting a board returned a
  meta-grounded `[COMP] Pivot to Vertical Cypher Vanguard…` referencing the
  curated comp by name.

### Honest limitations (next steps)
- In-game state is still entered manually (no overlay/ingestion).
- The meta file is hand-maintained per patch — update `set`/`patch`/`comps`.
- Restarting the API needs a page reload (or just toggle off/on) to re-connect.

---

## Enhancement: live meta from the Riot ladder (ToS-clean)

### Why this shape
- Replaces the hand-maintained curated file as the *primary* source with a meta
  **computed from the top of the Riot ranked ladder** — no third-party scraping,
  only official endpoints. The curated file becomes the resilient **fallback**.
- Sampling the ladder is rate-limited and slow, so it must never block a
  coaching call: results are cached and refreshed in the background, and callers
  get the last good snapshot (or the curated file) immediately.

### What was added
- **Application:** `IRiotLeagueClient.GetChallengerPuuidsAsync(maxPlayers)` —
  pulls the Challenger ladder ordered by LP.
- **Infrastructure:** `RiotLeagueClient` implements the challenger call
  (`tft/league/v1/challenger?queue=RANKED_TFT`). New `RiotMetaProvider`
  (`IMetaProvider`) samples top Challenger players, pulls their recent ranked
  matches, aggregates the best-placing trait+carry combos into a `MetaSnapshot`,
  caches with a TTL (`RiotMetaOptions`), refreshes in the background, and falls
  back to the injected `FileMetaProvider` when cold or on failure.
- **API:** `Program.cs` wires `RiotMetaProvider` (with separate KR/asia-routed
  `RiotMetaLeagueClient` + `RiotMetaMatchClient` named HTTP clients) as the
  `IMetaProvider` singleton when a Riot key is present, else the curated file
  only. `META_PLATFORM`/`META_REGION` env overrides (default `kr`/`asia`).
- **Tests:** `RiotMetaProviderTests` — fallback-when-cold, dominant-set
  filtering, aggregation/tiering. 48 pass total.

### Status
- Builds (`dotnet build`) and all 48 tests pass; frontend builds. The live meta
  is sampled from the KR Challenger ladder with the curated file as fallback.

### Honest limitations (next steps)
- First request after a cold start serves the curated fallback while the
  background ladder computation warms up (TTL-cached for 6h thereafter).
- Aggregation is a simple trait+carry heuristic, not a full comp classifier.
