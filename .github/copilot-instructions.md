# TFT Coach — Project Guidelines

Local-first AI coaching app for Teamfight Tactics. See [plan (1).md](../plan%20(1).md) for full architecture.

## Stack
- Backend: .NET 10 / ASP.NET Core, SignalR
- Inference: LM Studio (local, OpenAI-compatible API) — **never** add cloud/OpenAI endpoints
- Data: Supabase (Postgres + pgvector + Auth)
- Frontend: React + Vite, `@microsoft/signalr`

## Git Workflow
- **Every new feature starts on a new branch.** Never commit features directly to `main`.
- Branch naming: `feature/<short-description>` (e.g. `feature/get-suggestion-usecase`).
- Create the branch BEFORE writing code: `git checkout -b feature/<name>`.
- Keep one feature per branch; open a PR back to `main`.
- **Always break a feature into a small, ordered task list before coding** and work through it one task at a time.
- **Always create a plan file** at `docs/plans/feature-<short-name>.md` capturing the feature goal, the ordered task list, and the affected layers before writing code; keep it updated as tasks complete.

## Architecture (Clean Architecture)
Dependency rule — dependencies point inward only:
- `Domain` → no dependencies on any other layer
- `Application` → depends only on `Domain`
- `Infrastructure` / `API` → may depend on `Application` + `Domain`
- **Never reference Infrastructure from Domain or Application.**
- Use cases live in `Application/UseCases/<Name>/` with the 3-file pattern: `<Name>UseCase.cs`, `<Name>Request.cs`, `<Name>Response.cs`.

## Coaching Output Rules
- Every suggestion starts with exactly one tag: `[ECONOMY]` `[BOARD]` `[ITEMS]` `[COMP]` `[SCOUT]`.
- Max 2 sentences; first is an action verb (Sell, Buy, Roll, Hold, Move, Build, Pivot, Scout).
- Suggestion calls are stateless — no conversational history.

## Conventions
- Wrap all external calls (Riot API, LM Studio, Supabase) in try/catch with structured logging.
- Tag all patch documents with the TFT patch version.
- Tests: xUnit, AAA layout, naming `Method_ShouldExpected_WhenCondition`.

## Build & Test
- Build: `dotnet build`
- Test: `dotnet test`
- Frontend: `npm run dev` (in the React app folder)
- Local Supabase: `supabase start`
