import { useState } from 'react';
import { useOutletContext } from 'react-router-dom';
import { fetchMatchHistory, fetchRanks } from './api';
import type { AppContext, HistoryState } from './Layout';
import { RankClimbChart } from './components/RankClimbChart';

function SearchIcon() {
  return (
    <svg className="search-icon" viewBox="0 0 24 24" fill="none" aria-hidden="true">
      <circle cx="11" cy="11" r="7" stroke="currentColor" strokeWidth="2" />
      <path d="m20 20-3.5-3.5" stroke="currentColor" strokeWidth="2" strokeLinecap="round" />
    </svg>
  );
}

export function HistoryPage() {
  const { riotId, setRiotId, historyState, setHistoryState } = useOutletContext<AppContext>();
  const { matches, ranks, history, searched } = historyState;
  const update = (patch: Partial<HistoryState>) => setHistoryState((s) => ({ ...s, ...patch }));
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function handleSubmit(event: React.FormEvent) {
    event.preventDefault();
    const trimmed = riotId.trim();
    if (trimmed.length === 0) {
      return;
    }

    setLoading(true);
    setError(null);
    update({ searched: true, matches: [], ranks: null, history: [] });

    // Current-season matches power the estimated line; recorded snapshots add
    // accurate points that accumulate over time.
    const matchesPromise = fetchMatchHistory(trimmed, 0).catch(() => null);

    try {
      const rankResult = await fetchRanks(trimmed);
      update({ ranks: rankResult.entries, history: rankResult.history });
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to load rank history.');
    } finally {
      setLoading(false);
    }

    const matchResult = await matchesPromise;
    update({ matches: matchResult?.matches ?? [] });
  }

  const ranked = ranks?.find((entry) => entry.queueType === 'RANKED_TFT');
  const hasData = (ranked && matches.length > 0) || history.length > 0;

  return (
    <>
      <form className="search" onSubmit={handleSubmit}>
        <div className="search-field">
          <SearchIcon />
          <input
            type="text"
            value={riotId}
            onChange={(e) => setRiotId(e.target.value)}
            placeholder="Riot ID, e.g. Faker#EUW"
            aria-label="Riot ID"
          />
        </div>
        <button type="submit" disabled={loading}>
          {loading ? (
            <span className="btn-spinner">
              <span className="spinner" aria-hidden="true" />
              Loading…
            </span>
          ) : (
            'Load history'
          )}
        </button>
      </form>

      {error && (
        <p className="error" role="alert">
          {error}
        </p>
      )}

      {!loading && hasData && (
        <section className="results" aria-live="polite">
          <RankClimbChart matches={matches} current={ranked} history={history} />
        </section>
      )}

      {!loading && searched && !hasData && !error && (
        <div className="empty">
          <h3>No ranked timeline yet</h3>
          <p>
            Play a few Ranked TFT games, then refresh rank snapshots to build a reliable climb timeline.
          </p>
        </div>
      )}

      {!searched && !error && (
        <div className="empty">
          <h3>Rank climb tracker</h3>
          <p>Search Riot ID#TAG to chart LP movement across recent ranked matches.</p>
        </div>
      )}
    </>
  );
}
