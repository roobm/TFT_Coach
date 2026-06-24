-- Store full per-match detail so fetched matches can be served from the database
-- instead of re-querying the Riot API (which is throttled to 100 req / 2 min).
-- With traits + units cached, analysis keeps full fidelity off the cached rows.

alter table match_history add column if not exists traits             text[];
alter table match_history add column if not exists units              text[];
alter table match_history add column if not exists duration_seconds   integer;
alter table match_history add column if not exists players_eliminated integer;
