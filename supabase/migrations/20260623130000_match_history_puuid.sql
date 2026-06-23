-- Let match_history store fetched matches keyed by player PUUID (no user auth yet)
-- so match-history analysis can persist results and track them over time.

alter table match_history add column if not exists puuid    varchar(100);
alter table match_history add column if not exists match_id varchar(32);
alter table match_history add column if not exists level    integer;

-- Idempotent persistence: one row per (puuid, match_id).
create unique index if not exists match_history_puuid_match_idx
    on match_history (puuid, match_id);

-- Lookups by player.
create index if not exists match_history_puuid_idx
    on match_history (puuid);
