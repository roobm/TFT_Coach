-- Enrich match_history with per-player economy/tempo/damage signals and store
-- units as structured jsonb ({name, tier, items}) instead of a flat name array.

alter table match_history
  add column if not exists gold_left integer,
  add column if not exists last_round integer,
  add column if not exists total_damage_to_players integer;

-- ALTER COLUMN ... USING cannot contain a subquery, so convert via a temp column.
alter table match_history
  add column if not exists units_jsonb jsonb not null default '[]'::jsonb;

-- Preserve existing character ids as objects (tier/items unknown for old rows).
update match_history
  set units_jsonb = coalesce(
    (
      select jsonb_agg(jsonb_build_object('name', elem, 'tier', 0, 'items', '[]'::jsonb))
      from unnest(units) as elem
    ),
    '[]'::jsonb
  )
  where units is not null;

alter table match_history drop column if exists units;
alter table match_history rename column units_jsonb to units;
