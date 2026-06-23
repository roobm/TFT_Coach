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
| Game state   | Riot Live Client Data API + TFT log files          |

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

Endpoints:

- `GET /matches?riotId=Name%23TAG&count=10` — TFT match history.
- `POST /matches/analysis` — LLM insights for a set of matches.

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
  TFTCoach.Infrastructure/   # Riot API, LM Studio, Supabase implementations
  TFTCoach.API/              # ASP.NET Core endpoints / SignalR
  TFTCoach.Seeder/           # CLI tool to embed patch data
frontend/                    # React + Vite client
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