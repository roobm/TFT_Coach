-- Track ranked-only filtering for the climb estimate, and record point-in-time
-- rank snapshots so the app can build an accurate climb history over time
-- (Riot's API does not expose historical rank/LP).

-- Riot queue id per cached match (e.g. 1100 = Ranked TFT, 1160 = Ranked Double Up).
alter table match_history
  add column if not exists queue_id integer;

-- Point-in-time ranked standing, captured on each /ranks fetch.
create table if not exists rank_history (
    id            bigserial primary key,
    puuid         varchar(100) not null,
    queue_type    text         not null,
    tier          text,
    division      text,
    league_points integer      not null default 0,
    wins          integer      not null default 0,
    losses        integer      not null default 0,
    recorded_at   timestamptz  not null default now()
);

-- Read a player's history per queue, oldest first.
create index if not exists rank_history_puuid_queue_recorded_idx
    on rank_history (puuid, queue_type, recorded_at);
