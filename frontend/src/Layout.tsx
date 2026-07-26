import { useEffect, useState } from 'react';
import { NavLink, Outlet } from 'react-router-dom';
import type {
  AnalysisResponse,
  RankedEntry,
  RankSnapshot,
  SeasonBestRank,
  TftMatchResult,
} from './api';

/** Persisted result state for the Matches page, kept here so it survives navigation. */
export interface MatchesState {
  count: number;
  currentSet: number;
  resultSet: number;
  matches: TftMatchResult[];
  puuid: string | null;
  ranks: RankedEntry[] | null;
  rankHistory: RankSnapshot[];
  searchedRiotId: string;
  analysis: AnalysisResponse | null;
}

/** Persisted result state for the History page, kept here so it survives navigation. */
export interface HistoryState {
  season: number;
  currentSet: number;
  allSeasonMatches: TftMatchResult[];
  matches: TftMatchResult[];
  ranks: RankedEntry[] | null;
  history: RankSnapshot[];
  bestBySeason: SeasonBestRank[];
  searched: boolean;
}

/** Shared layout state available to child routes via `useOutletContext`. */
export interface AppContext {
  riotId: string;
  setRiotId: (value: string) => void;
  matchesState: MatchesState;
  setMatchesState: React.Dispatch<React.SetStateAction<MatchesState>>;
  historyState: HistoryState;
  setHistoryState: React.Dispatch<React.SetStateAction<HistoryState>>;
}

const RIOT_ID_KEY = 'tftCoach.riotId';

const initialMatchesState: MatchesState = {
  count: 20,
  currentSet: 0,
  resultSet: 0,
  matches: [],
  puuid: null,
  ranks: null,
  rankHistory: [],
  searchedRiotId: '',
  analysis: null,
};

const initialHistoryState: HistoryState = {
  season: 0,
  currentSet: 0,
  allSeasonMatches: [],
  matches: [],
  ranks: null,
  history: [],
  bestBySeason: [],
  searched: false,
};

function navClass({ isActive }: { isActive: boolean }): string {
  return `nav-link ${isActive ? 'active' : ''}`;
}

export function Layout() {
  // Riot ID and the per-page results are lifted here so they persist when
  // switching between Matches / History / Comps / Live Coach. The Riot ID is also
  // persisted to localStorage so the remembered account survives a full reload.
  const [riotId, setRiotId] = useState(() => localStorage.getItem(RIOT_ID_KEY) ?? '');
  const [matchesState, setMatchesState] = useState<MatchesState>(initialMatchesState);
  const [historyState, setHistoryState] = useState<HistoryState>(initialHistoryState);

  useEffect(() => {
    localStorage.setItem(RIOT_ID_KEY, riotId);
  }, [riotId]);

  return (
    <main className="app">
      <header className="app-header">
        <span className="brand-mark" aria-hidden="true">S</span>
        <div className="app-header-text">
          <h1>TFT Coach</h1>
          <p className="subtitle">Patch-aware local coaching for ranked TFT decisions.</p>
        </div>
        <nav className="app-nav" aria-label="Primary">
          <NavLink to="/" end className={navClass}>Matches</NavLink>
          <NavLink to="/history" className={navClass}>History</NavLink>
          <NavLink to="/comps" className={navClass}>Comps</NavLink>
          <NavLink to="/live-coach" className={navClass}>Live Coach</NavLink>
        </nav>
      </header>

      <Outlet
        context={{
          riotId,
          setRiotId,
          matchesState,
          setMatchesState,
          historyState,
          setHistoryState,
        } satisfies AppContext}
      />
    </main>
  );
}
