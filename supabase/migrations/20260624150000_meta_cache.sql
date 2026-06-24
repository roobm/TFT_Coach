-- Cache of the live, ladder-derived TFT meta, grouped by set. One row per set
-- (upserted), so the app can serve the last computed meta instantly after a
-- restart instead of recomputing from the Riot ladder on every cold start.

create table if not exists meta_cache (
    set_key     text primary key,        -- e.g. "Set 17"
    patch       varchar(20) not null,
    comps       jsonb       not null,     -- [{ name, tier, playstyle, carries, keyItems, notes }]
    computed_at timestamptz not null default now()
);

-- Serve the most recently computed set quickly.
create index if not exists meta_cache_computed_at_idx
    on meta_cache (computed_at desc);
