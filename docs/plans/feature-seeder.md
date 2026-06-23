# Feature: Patch Data Seeder (Phase 3)

## Goal
Populate `tft_patch_data` with embedded TFT knowledge: fetch champion/item/trait
data from Riot's Data Dragon, chunk + summarize it, embed each chunk via LM Studio,
and upsert the vectors into Supabase. Runnable as a CLI with a `--patch` flag.

## Affected Layers
- **Domain**: `PatchDocument` entity, `IVectorRepository` interface.
- **Application**: `ILMStudioClient`, `IPatchContentProvider`, `SeedPatchData` use case (3-file).
- **Infrastructure**: `DataDragonService` (content provider + chunker), `LMStudioClient`
  (embeddings), `SupabaseVectorRepository` (Npgsql upsert with pgvector).
- **Seeder console** (`src/TFTCoach.Seeder`): entry point, DI wiring, `--patch` arg.

## Task List
- [x] Domain: `PatchDocument` entity + `IVectorRepository`.
- [x] Application: `ILMStudioClient`, `IPatchContentProvider`, `SeedPatchData` use case.
- [x] Infrastructure: `DataDragonService` (fetch Data Dragon JSON → chunked docs).
- [x] Infrastructure: patch-note chunker helper.
- [x] Infrastructure: `LMStudioClient` (`/v1/embeddings`).
- [x] Infrastructure: `SupabaseVectorRepository` (upsert into `tft_patch_data`).
- [x] Console seeder project with `--patch 14.x` flag + DI.
- [x] Unit tests: chunker + `SeedPatchData` use case (fakes for client/repo).
- [ ] `dotnet build` + `dotnet test`, commit, open PR.

## Notes
- **Inference is local only** — LM Studio at `LM_STUDIO_URL`, no cloud endpoints.
- All external calls (Data Dragon HTTP, LM Studio, Supabase) wrapped in try/catch
  with structured logging per project conventions.
- **Embedding dimension**: schema column is `vector(1536)`. The chosen LM Studio
  embedding model MUST output 1536 dims (or the schema/migration must be adjusted).
  The use case validates dimension before upsert and fails fast on mismatch.
- `--patch` re-seeds `tft_patch_data` only; never touches `match_history`.
