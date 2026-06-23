# Feature: Riot Match History + AI Insights

## Goal
Fetch a player's recent TFT matches from the Riot API (by Riot ID), expose them
through the backend, render a match-history page in a new React app, and generate
LM Studio-powered insights ("assumptions") about the player's tendencies.

## Decisions
- Scope: backend endpoints **+** minimal React page.
- Storage: **pass-through** (no DB writes yet) — Riot is queried per request.
- Routing/region: **europe** for Account-V1 + TFT-Match-V1.
- Identity: **Riot ID** (`gameName#tagLine`) → PUUID via Account-V1.
- Inference: **LM Studio only** (local) for the insights summary.

## Affected Layers
- **Domain**: `TftMatchResult` (placement, comp/traits, augments, level, etc.).
- **Application**:
  - `IRiotMatchClient` interface.
  - `GetMatchHistory` use case (Riot ID → recent `TftMatchResult` list).
  - `ILMStudioClient.CompleteAsync(...)` (new chat-completion method).
  - `AnalyzeMatchHistory` use case (match list → LM Studio insight text).
- **Infrastructure**:
  - `RiotMatchClient` — Account-V1 (`/riot/account/v1/accounts/by-riot-id`) +
    TFT-Match-V1 (`/tft/match/v1/matches/by-puuid/{puuid}/ids`, `/matches/{id}`).
  - `LMStudioClient.CompleteAsync` — `/v1/chat/completions`.
- **API**: `GET /matches?riotId=Name%23TAG&count=10`,
  `POST /matches/analysis` (matches → insight). CORS for the Vite dev origin.
- **Frontend** (`frontend/`): Vite + React + TypeScript. Match list + "Analyze" button.

## Task List
- [x] Domain: `TftMatchResult`.
- [x] Application: `IRiotMatchClient` + `GetMatchHistory` use case.
- [x] Application: extend `ILMStudioClient` with `CompleteAsync`.
- [x] Application: `AnalyzeMatchHistory` use case.
- [x] Infrastructure: `RiotMatchClient` (Account-V1 + TFT-Match-V1, europe routing).
- [x] Infrastructure: `LMStudioClient.CompleteAsync` (chat completions).
- [x] API: `/matches` + `/matches/analysis` minimal endpoints, DI, CORS.
- [x] Frontend: scaffold Vite React TS app under `frontend/`.
- [x] Frontend: match-history page + AI insights panel.
- [x] Unit tests: `GetMatchHistory` + `AnalyzeMatchHistory` (fakes).
- [ ] `dotnet build` + `dotnet test`, commit, open PR.

## Notes
- **`RIOT_API_KEY` required** (`RGAPI-...`). Development keys expire every 24h.
  Wrap all Riot calls in try/catch with structured logging; handle 404 (unknown
  Riot ID) and 429 (rate limit) explicitly.
- TFT-Match-V1 match list IDs use the regional host (e.g. `europe.api.riotgames.com`).
- The insight prompt asks for short, factual observations grounded only in the
  supplied match data (no invented facts), consistent with the coaching rules.
- AI insights are advisory; no DB persistence in this slice.
