import { useState } from 'react';
import { NavLink, Outlet } from 'react-router-dom';

/** Shared layout state available to child routes via `useOutletContext`. */
export interface AppContext {
  riotId: string;
  setRiotId: (value: string) => void;
}

function navClass({ isActive }: { isActive: boolean }): string {
  return `nav-link ${isActive ? 'active' : ''}`;
}

export function Layout() {
  // Riot ID is lifted here so it persists when switching between Matches/History.
  const [riotId, setRiotId] = useState('');

  return (
    <main className="app">
      <header className="app-header">
        <span className="brand-mark" aria-hidden="true">TC</span>
        <div className="app-header-text">
          <h1>TFT Coach</h1>
          <p className="subtitle">Recent Teamfight Tactics matches with local AI insights.</p>
        </div>
        <nav className="app-nav" aria-label="Primary">
          <NavLink to="/" end className={navClass}>Matches</NavLink>
          <NavLink to="/history" className={navClass}>History</NavLink>
          <NavLink to="/live-coach" className={navClass}>Live Coach</NavLink>
        </nav>
      </header>

      <Outlet context={{ riotId, setRiotId } satisfies AppContext} />
    </main>
  );
}
