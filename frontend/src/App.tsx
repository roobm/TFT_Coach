import { useState } from 'react';
import { useOutletContext } from 'react-router-dom';
import {
  analyzeMatches,
  fetchMatchHistory,
  fetchRanks,
  type AnalysisResponse,
  type RankedEntry,
  type RankSnapshot,
  type TftMatchResult,
} from './api';
import type { AppContext } from './Layout';
import { AnalysisPanel } from './components/AnalysisPanel';
import { MatchList } from './components/MatchList';
import { MatchListSkeleton } from './components/MatchListSkeleton';
import { MatchStats } from './components/MatchStats';
import { RankCard } from './components/RankCard';
import { RankClimbChart } from './components/RankClimbChart';

// Previous TFT sets offered in the season picker (newest current set minus N).
const SEASON_LOOKBACK = 6;

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
  const { riotId, setRiotId } = useOutletContext<AppContext>();
  const [count, setCount] = useState(20);
  const [season, setSeason] = useState(0); // 0 = current season
  const [currentSet, setCurrentSet] = useState(0);
  const [resultSet, setResultSet] = useState(0);
  const [matches, setMatches] = useState<TftMatchResult[]>([]);
  const [puuid, setPuuid] = useState<string | null>(null);
  const [ranks, setRanks] = useState<RankedEntry[] | null>(null);
  const [rankHistory, setRankHistory] = useState<RankSnapshot[]>([]);
  const [searchedRiotId, setSearchedRiotId] = useState('');
  const [analysis, setAnalysis] = useState<AnalysisResponse | null>(null);
  const [loadingMatches, setLoadingMatches] = useState(false);
  const [loadingAnalysis, setLoadingAnalysis] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function runSearch(nextCount: number, nextSeason: number) {
    const trimmed = riotId.trim();
    if (trimmed.length === 0) {
      return;
    }

    setLoadingMatches(true);
    setError(null);
    setAnalysis(null);
    setMatches([]);
    setPuuid(null);
    setRanks(null);
    setRankHistory([]);
    setSearchedRiotId(trimmed);

    // Ranks are a cheap, independent call — fetch them alongside match history.
    const ranksPromise = fetchRanks(trimmed).catch(() => null);

    try {
      const result = await fetchMatchHistory(trimmed, nextCount, nextSeason);
      setMatches(result.matches);
      setPuuid(result.puuid);
      setCurrentSet(result.currentSet);
      setResultSet(result.set);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to load matches.');
    } finally {
      setLoadingMatches(false);
    }

    const rankResult = await ranksPromise;
    setRanks(rankResult?.entries ?? null);
    setRankHistory(rankResult?.history ?? []);
  }

  function handleSearch(event: React.FormEvent) {
    event.preventDefault();
    void runSearch(count, season);
  }

  // Switching season is an explicit "show me that season" action — load it now.
  function handleSeasonChange(nextSeason: number) {
    setSeason(nextSeason);
    if (searchedRiotId.length > 0) {
      void runSearch(count, nextSeason);
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

  const seasonOptions = buildSeasonOptions(currentSet);

  return (
    <>
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
          value={season}
          onChange={(e) => handleSeasonChange(Number(e.target.value))}
          aria-label="Season"
          disabled={loadingMatches}
        >
          {seasonOptions.map(({ value, label }) => (
            <option key={value} value={value}>
              {label}
            </option>
          ))}
        </select>
        <select
          className="count-select"
          value={count}
          onChange={(e) => setCount(Number(e.target.value))}
          aria-label="Number of matches"
          disabled={loadingMatches}
        >
          {[
            { value: 10, label: '10 games' },
            { value: 20, label: '20 games' },
            { value: 50, label: '50 games' },
            { value: 100, label: '100 games' },
            { value: 200, label: '200 games' },
            { value: 0, label: 'All this season' },
          ].map(({ value, label }) => (
            <option key={value} value={value}>
              {label}
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
              <span className="count">{matches.length}</span> games
              {resultSet > 0 && <span className="results-season"> · Set {resultSet}</span>}
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

          {ranks && <RankCard riotId={searchedRiotId} entries={ranks} />}

          {ranks && (
            <RankClimbChart
              matches={matches}
              current={ranks.find((entry) => entry.queueType === 'RANKED_TFT')}
              history={rankHistory}
            />
          )}

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
    </>
  );
}

// Season picker options: "This season" plus the previous few sets, fetched on
// demand. Older sets are only known once the current set is resolved by a search.
function buildSeasonOptions(currentSet: number): { value: number; label: string }[] {
  const options = [
    { value: 0, label: currentSet > 0 ? `This season (Set ${currentSet})` : 'This season' },
  ];
  for (let set = currentSet - 1; set >= Math.max(1, currentSet - SEASON_LOOKBACK); set--) {
    options.push({ value: set, label: `Set ${set}` });
  }
  return options;
}
