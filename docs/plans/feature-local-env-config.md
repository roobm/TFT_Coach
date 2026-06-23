# Feature: Local Environment Config

## Goal
Complete the remaining Phase 1 / early Phase 2 environment setup that is scriptable:
initialize local Supabase config and provide a tracked `.env.example` template so any
developer can configure their local environment. Document the manual steps that cannot be
automated (starting Docker, loading + benchmarking an LM Studio model).

## Affected Layers
- Repo root / tooling (no application code)
- `supabase/` local config
- `.env.example`

## Task List
- [x] Create branch `feature/local-env-config`.
- [x] Write this plan file.
- [x] Run `supabase init` to generate local Supabase config.
- [x] Create `.env.example` with the variables from plan Section 10 (no secrets).
- [x] Attempt `supabase start` (requires Docker daemon running) — succeeded; stack runs on `127.0.0.1:54321`.
- [x] Document manual steps: start Docker Desktop, load an LM Studio model, benchmark latency < 3s.

## Manual / Blocked Steps (cannot be automated here)
- **Docker**: `supabase start` needs the Docker daemon running. Start Docker Desktop first.
- **LM Studio**: install is done; loading a model (`llama3.1:8b` or similar) and starting the
  local server (port 1234) plus running a latency benchmark must be done in the LM Studio app.

## Notes
- `.env` itself stays git-ignored; only `.env.example` is tracked.
- No cloud inference — LM Studio local endpoint only.
- This Supabase version issues **publishable/secret keys** (e.g. `sb_publishable_...`,
  `sb_secret_...`) rather than the legacy `anon`/`service_role` JWTs; `.env.example` reflects this.
- Run `supabase status` at any time to reprint local URLs and keys. Use `supabase stop` to shut down.
