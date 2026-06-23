-- Seed data for local development / verification.

-- One test patch document with a deterministic embedding so we can verify
-- that pgvector similarity search returns it. The embedding is a constant
-- 1536-dim vector; real embeddings are produced by the seeder (Phase 3).
insert into tft_patch_data (patch, category, content, embedding)
values (
    '14.1',
    'patch_note',
    'Test patch note: economy changes and champion tuning for verification.',
    array_fill(0.1::real, array[1536])::vector
);
