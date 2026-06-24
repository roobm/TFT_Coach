-- Riot's GameVersion string can be long (e.g. "Linux Version 15.13.700.8076 (...) [PUBLIC] <Releases/15.13>").
-- Store the raw patch identifier without a length cap to avoid truncation failures.
alter table match_history alter column patch type text;
