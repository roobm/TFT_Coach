# TFT Coach

A local-first AI coaching app for **Teamfight Tactics**. It delivers real-time
suggestions during a game and a post-game stats/recap page. All inference runs
**locally via LM Studio** — no cloud inference. User and patch data live in Supabase.

See [plan (1).md](plan%20(1).md) for the full architecture.

## Quick start

Once the [prerequisites](#prerequisites) are installed and Docker + LM Studio are running,
start Supabase, the API, and the frontend in one go:

```bash
./scripts/dev-start.sh
```

The script copies `.env.example` to `.env` on first run, starts local Supabase, checks
that LM Studio is reachable, then runs the backend and frontend together (Ctrl-C stops
them; Supabase keeps running — stop it with `supabase stop`). For the manual,
step-by-step version see the sections below.

## Stack

| Layer        | Technology                                         |
| ------------ | -------------------------------------------------- |
| Backend      | .NET 10 / ASP.NET Core, SignalR                    |
| Inference    | LM Studio (local, OpenAI-compatible API)           |
| Data         | Supabase (Postgres + pgvector + Auth)              |
| Frontend     | React + Vite + TypeScript                          |
| Game state   | Riot Live Client Data API                          |

## Prerequisites

Install these first:

- **.NET 10 SDK** — `dotnet --version` should print `10.x`.
- **Node.js 18+** and npm — for the React frontend.
- **Docker Desktop** — required by local Supabase. Start it before `supabase start`.
- **Supabase CLI** — https://supabase.com/docs/guides/local-development
- **LM Studio** — https://lmstudio.ai — load a chat model and an embedding model,
  then start its local server (port `1234`).
- A **Riot API key** (https://developer.riotgames.com) for match history.

## 1. Configure environment

Copy the template and fill in real values:

```bash
cp .env.example .env
```

`.env` is git-ignored. Local Supabase keys/URLs are printed by `supabase start`
(and `supabase status`). At minimum set `RIOT_API_KEY`, and confirm the
`LM_STUDIO_URL`, `EMBEDDING_MODEL`, and `CHAT_MODEL` values match your LM Studio setup.

Key variables:

| Variable | Default | Notes |
| --- | --- | --- |
| `RIOT_API_KEY` | *(required)* | Riot developer key |
| `RIOT_REGION` | `europe` | Regional routing host for match/account endpoints |
| `RIOT_PLATFORM` | `euw1` | Platform routing host for ranked (League-V1) endpoints |
| `LIVE_CLIENT_URL` | `http://host.docker.internal:2999` | Riot Live Client Data API used by the live-coach page |
| `LM_STUDIO_URL` | `http://host.docker.internal:1234/v1` | LM Studio server; use `http://localhost:1234/v1` outside Docker |
| `CHAT_MODEL` | `local-model` | Model loaded in LM Studio for coaching/analysis |
| `EMBEDDING_MODEL` | `text-embedding-nomic-embed-text-v1.5` | Embedding model (must output 1536 dims) |
| `EMBEDDING_DIM` | `1536` | Vector dimension expected by the schema |
| `META_PLATFORM` | `kr` | Platform used to sample the Challenger ladder for meta computation |
| `META_REGION` | `asia` | Regional host for meta match fetching |
| `META_MAX_PLAYERS` | *(default)* | How many ladder players to sample |
| `META_MATCHES_PER_PLAYER` | *(default)* | Matches fetched per player |

> When running the API/seeder **outside Docker**, use `http://localhost:1234/v1`
> for `LM_STUDIO_URL` instead of `host.docker.internal`.

## 2. Start Supabase (local)

```bash
supabase start      # requires Docker running; applies migrations in supabase/migrations
supabase status     # reprint local URLs + keys at any time
```

Defaults: API at `127.0.0.1:54321`, database at `127.0.0.1:54322`.
Stop the stack with `supabase stop`.

## 3. Start LM Studio

In the LM Studio app: load a chat model and an embedding model (the embedding model
must output **1536** dimensions to match the schema), then start the local server on
port `1234`. Aim for inference latency under ~3s.

## 4. Seed patch data (per patch)

Run on demand (e.g. patch day) to embed champion/item/patch content into pgvector:

```bash
dotnet run --project src/TFTCoach.Seeder -- --patch 14.1
```

Optional overrides: `--embedding-model <id>` and `--embedding-dim <n>` (default 1536).

## 5. Run the backend API

```bash
dotnet run --project src/TFTCoach.API
```

- HTTP: http://localhost:5214
- HTTPS: https://localhost:7101
- Swagger UI (Development): http://localhost:5214/swagger
- Swagger JSON (Development): http://localhost:5214/swagger/v1/swagger.json

REST endpoints:

- `GET /matches?riotId=Name%23TAG&count=10&set=17` — TFT match history. `set` is optional; omit to return all sets.
- `POST /matches/analysis` — LLM insights for a set of matches.
- `GET /ranks?riotId=Name%23TAG` — current ranked standing for a summoner.
- `POST /teamcode` — decode a TFT Team Planner share code into board units. Body: `{ "code": "<code>" }`.

Every REST response uses the same envelope:

```json
{
  "success": true,
  "data": { "...": "endpoint payload" },
  "error": null
}
```

Failed requests return:

```json
{
  "success": false,
  "data": null,
  "error": {
    "code": "validation.invalid_riot_id",
    "message": "riotId must be in the form gameName#tagLine."
  }
}
```

SignalR hub at `/hubs/coach` (use `@microsoft/signalr` in the frontend):

- `GetSuggestion(request)` → returns `{ success, data, error }` where `data` is one tagged coaching tip.
- `GetMeta()` → returns `{ success, data, error }` where `data` is the active meta or `null`.
- `GetMetaComps()` → returns `{ success, data, error }` where `data` is full current meta or `null`.

## 6. Run the frontend

```bash
cd frontend
npm install
npm run dev
```

Opens at http://localhost:5173. It calls the API at `http://localhost:5214`
(override with `VITE_API_BASE`). The API CORS policy allows `FRONTEND_ORIGIN`
(default `http://localhost:5173`).

## Build & test

```bash
dotnet build                 # build the solution
dotnet test                  # run all xUnit tests under tests/
cd frontend && npm run build # build the frontend
```

## Project layout

```
src/
  TFTCoach.Domain/           # entities + interfaces (no other dependencies)
  TFTCoach.Application/      # use cases (Domain only)
    UseCases/
      AnalyzeMatchHistory/   # LLM insights for match history
      DecodeTeamCode/        # decode TFT Team Planner share codes
      GetMatchHistory/       # fetch + store match history via Riot API
      GetRanks/              # ranked standing via Riot League-V1
      GetSuggestion/         # single live coaching suggestion via LM Studio
      SeedPatchData/         # embed patch content into pgvector
  TFTCoach.Infrastructure/   # Riot API, LM Studio, Supabase implementations
    InferenceService/        # LM Studio client + meta providers
    Meta/                    # meta computation (Riot ladder + file fallback)
    Persistence/             # Supabase repositories
    RiotApi/                 # Riot match/league/live-client clients
  TFTCoach.API/              # ASP.NET Core endpoints + SignalR hub
    Routes/ApiEndpoints.cs   # REST endpoint mappings + Swagger summaries/descriptions
    Hubs/CoachHub.cs         # /hubs/coach — GetSuggestion, GetMeta, GetMetaComps
  TFTCoach.Seeder/           # CLI tool to embed patch data
frontend/                    # React + Vite client
  src/
    LiveCoachPage.tsx        # real-time coaching via SignalR
    HistoryPage.tsx          # match history + LLM analysis
    CompsPage.tsx            # current meta comps
    coachHub.ts              # typed SignalR client wrapper
    api.ts                   # REST API client
supabase/                    # local config + migrations
docs/plans/                  # per-feature plan files
tests/                       # xUnit test projects
```

## Conventions

- Clean Architecture: dependencies point inward; never reference Infrastructure
  from Domain or Application.
- **LM Studio only** — never add cloud/OpenAI inference endpoints.
- Every feature starts on its own `feature/<name>` branch with a plan file in
  `docs/plans/`.