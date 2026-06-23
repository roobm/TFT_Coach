# Feature: Supabase Schema (Phase 2)

## Goal
Create the database schema for TFT Coach in local Supabase: enable `pgvector`,
create all core tables, and verify vector similarity search works end-to-end.

## Affected Layers
- Infrastructure / data only — SQL migrations under `supabase/migrations/`.
- No Domain/Application/API code in this slice.

## Tables (see plan Section 4)
- `tft_patch_data` — patch/champion knowledge, `vector(1536)` embedding.
- `match_history` — per-game structured stats, FK to `auth.users`.
- `round_logs` — round-level detail, `vector(1536)` embedding, FK to `match_history`.
- `profiles` — user profile + paid flag, PK FK to `auth.users`.

## Task List
- [x] Migration: enable `vector` extension.
- [x] Migration: create `tft_patch_data`, `match_history`, `round_logs`, `profiles`.
- [x] Seed: insert one test patch document with a known embedding.
- [x] Apply migrations to local Supabase (`supabase migration up` / `db reset`).
- [x] Verify vector search returns the seeded row via `<->` distance query.
- [ ] Commit and open PR.

## Notes
- Embeddings are `vector(1536)` to match the planned embedding model dimension.
- Migrations are forward-only; use `supabase db reset` locally to re-apply cleanly.
