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
