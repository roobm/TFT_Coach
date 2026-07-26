# Feature: Best ranks per season on History page

## Goal
Show a player's **best achieved rank per TFT season (set)** on the History page.

## Affected layers
- **Domain/Application**: expose season-best ranked milestones from rank history.
- **Infrastructure**: derive per-set best rank from stored rank snapshots + cached matches.
- **Frontend**: render a "Best ranks by season" section on `/history`.

## Design
1. Keep `/ranks` as the source for History page rank data.
2. Extend the ranks response with `bestBySeason`.
3. Compute `bestBySeason` in `IRankHistoryRepository`:
   - use `rank_history` snapshots from `RANKED_TFT`.
   - map each snapshot to a TFT set using cached `match_history` timestamps.
   - select the highest ladder point per set.
4. Render the list in History UI sorted by newest set first.

## Ordered task list
1. [x] Add backend response model for season-best ranks and wire it in use case.
2. [x] Implement repository query + ranking selection per set.
3. [x] Extend tests for GetRanks use case and fakes.
4. [x] Extend frontend API types and History page UI.
5. [x] Add matching styles for the new history section.
6. [x] Build and test (`dotnet build`, `dotnet test`, `npm run build`).

## Status
- Complete.

## Follow-up
- Updated History loading to backfill all seasons (`/matches` with `set=-1`) while keeping the chart scoped to current-season matches only.
- Moved the season dropdown from Matches page to History page, where season switching now reuses the already loaded all-season match dataset.
