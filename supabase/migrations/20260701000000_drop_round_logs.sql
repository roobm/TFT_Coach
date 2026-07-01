-- round_logs was intended for a TFT log-file watcher feature that was removed
-- because reading client log files violates the Riot Games Terms of Service.
-- Drop the table and its index so the schema stays consistent with the plan.

drop index if exists round_logs_embedding_idx;
drop table if exists round_logs;
