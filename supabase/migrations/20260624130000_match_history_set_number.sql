-- Record the TFT set (season) per cached match so match history can be filtered
-- to the current set instead of pulling every season the player has ever played.

alter table match_history
  add column if not exists set_number integer;
