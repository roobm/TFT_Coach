import { useState } from 'react';
import {
  analyzeMatches,
  fetchMatchHistory,
  type AnalysisResponse,
  type TftMatchResult,
} from './api';
import { AnalysisPanel } from './components/AnalysisPanel';
import { MatchList } from './components/MatchList';
import { MatchListSkeleton } from './components/MatchListSkeleton';
import { MatchStats } from './components/MatchStats';

function SearchIcon() {
  return (
    <svg className="search-icon" viewBox="0 0 24 24" fill="none" aria-hidden="true">
      <circle cx="11" cy="11" r="7" stroke="currentColor" strokeWidth="2" />
      <path d="m20 20-3.5-3.5" stroke="currentColor" strokeWidth="2" strokeLinecap="round" />
    </svg>
  );
}

function ErrorIcon() {
  return (
    <svg viewBox="0 0 24 24" width="18" height="18" fill="none" aria-hidden="true">
      <circle cx="12" cy="12" r="9" stroke="currentColor" strokeWidth="2" />
      <path d="M12 7v6" stroke="currentColor" strokeWidth="2" strokeLinecap="round" />
      <circle cx="12" cy="16.5" r="1.1" fill="currentColor" />
    </svg>
  );
}

function EmptyIcon() {
  return (
    <svg className="empty-icon" viewBox="0 0 24 24" fill="none" aria-hidden="true">
      <rect x="3" y="4" width="18" height="16" rx="2" stroke="currentColor" strokeWidth="1.6" />
      <path d="M7 9h10M7 13h6" stroke="currentColor" strokeWidth="1.6" strokeLinecap="round" />
    </svg>
  );
}

export default function App() {
  const [riotId, setRiotId] = useState('');
  const [count, setCount] = useState(20);
  const [matches, setMatches] = useState<TftMatchResult[]>([]);
  const [puuid, setPuuid] = useState<string | null>(null);
  const [analysis, setAnalysis] = useState<AnalysisResponse | null>(null);
  const [loadingMatches, setLoadingMatches] = useState(false);
  const [loadingAnalysis, setLoadingAnalysis] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function handleSearch(event: React.FormEvent) {
    event.preventDefault();
    if (riotId.trim().length === 0) {
      return;
    }

    setLoadingMatches(true);
    setError(null);
    setAnalysis(null);
    setMatches([]);
    setPuuid(null);

    try {
      const result = await fetchMatchHistory(riotId.trim(), count);
      setMatches(result.matches);
      setPuuid(result.puuid);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to load matches.');
    } finally {
      setLoadingMatches(false);
    }
  }

  async function handleAnalyze() {
    setLoadingAnalysis(true);
    setError(null);

    try {
      const result = await analyzeMatches(matches, puuid ?? undefined);
      setAnalysis(result);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to analyze matches.');
    } finally {
      setLoadingAnalysis(false);
    }
  }

  return (
    <main className="app">
      <header className="app-header">
        <span className="brand-mark" aria-hidden="true">TC</span>
        <div>
          <h1>TFT Coach</h1>
          <p className="subtitle">Recent Teamfight Tactics matches with local AI insights.</p>
        </div>
      </header>

      <form className="search" onSubmit={handleSearch}>
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
        <select
          className="count-select"
          value={count}
          onChange={(e) => setCount(Number(e.target.value))}
          aria-label="Number of matches"
          disabled={loadingMatches}
        >
          {[10, 20, 50, 100, 200].map((n) => (
            <option key={n} value={n}>
              {n} games
            </option>
          ))}
        </select>
        <button type="submit" disabled={loadingMatches}>
          {loadingMatches ? (
            <span className="btn-spinner">
              <span className="spinner" aria-hidden="true" />
              Loading…
            </span>
          ) : (
            'Search'
          )}
        </button>
      </form>

      {error && (
        <p className="error" role="alert">
          <ErrorIcon />
          {error}
        </p>
      )}

      {loadingMatches && (
        <section aria-busy="true" aria-label="Loading matches">
          <MatchListSkeleton />
        </section>
      )}

      {!loadingMatches && matches.length > 0 && (
        <section className="results" aria-live="polite">
          <div className="results-header">
            <h2>
              <span className="count">{matches.length}</span> games played
            </h2>
            <button type="button" onClick={handleAnalyze} disabled={loadingAnalysis}>
              {loadingAnalysis ? (
                <span className="btn-spinner">
                  <span className="spinner" aria-hidden="true" />
                  Analyzing…
                </span>
              ) : (
                'Analyze with AI'
              )}
            </button>
          </div>

          {analysis && <AnalysisPanel analysis={analysis} />}

          <MatchStats matches={matches} />

          <MatchList matches={matches} />
        </section>
      )}

      {!loadingMatches && matches.length === 0 && !error && (
        <div className="empty">
          <EmptyIcon />
          <h3>No matches yet</h3>
          <p>Search for a Riot ID above to see recent matches and AI insights.</p>
        </div>
      )}
    </main>
  );
}
