# Feature: Getting-Started README

## Goal
Replace the placeholder root `README.md` with clear instructions on how to start the
project locally: prerequisites, environment setup, starting Supabase + LM Studio, running
the seeder, the API, and the frontend, plus build/test commands.

## Affected Layers
- Repo root documentation only (`README.md`)
- No application code changes

## Task List
- [x] Create branch `feature/readme-getting-started`.
- [x] Write this plan file.
- [x] Write the getting-started `README.md` (prereqs, env, run order, commands, ports).
- [x] Verify `dotnet build` / `dotnet test` still pass (docs-only change should not break).

## Notes
- LM Studio only — no cloud inference referenced.
- Ports: API `5214` (http) / `7101` (https), frontend `5173`, LM Studio `1234`,
  Supabase API `54321` / db `54322`, Live Client Data API `2999`.
- `.env` stays git-ignored; developers copy `.env.example`.
