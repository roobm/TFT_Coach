import { useCallback, useEffect, useState } from 'react';
import { connect, getMetaComps, type MetaComp, type MetaDetail } from './coachHub';

// Highest tier first; anything unrecognised sorts to the end.
const TIER_ORDER: Record<string, number> = { S: 0, A: 1, B: 2, C: 3, D: 4 };

function tierRank(tier: string): number {
  return TIER_ORDER[tier.trim().toUpperCase()] ?? 99;
}

function CompCard({ comp }: { comp: MetaComp }) {
  const tier = comp.tier.trim().toUpperCase() || '?';
  return (
    <article className="comp-card">
      <header className="comp-card-head">
        <span className={`comp-tier tier-${tier.toLowerCase()}`}>{tier}</span>
        <h3 className="comp-name">{comp.name}</h3>
      </header>

      {comp.carries.length > 0 && (
        <div className="comp-row">
          <span className="comp-row-label">Carries</span>
          <ul className="chip-list">
            {comp.carries.map((c) => (
              <li key={c} className="chip chip-carry">{c}</li>
            ))}
          </ul>
        </div>
      )}

      {comp.keyItems.length > 0 && (
        <div className="comp-row">
          <span className="comp-row-label">Key items</span>
          <ul className="chip-list">
            {comp.keyItems.map((i) => (
              <li key={i} className="chip chip-item">{i}</li>
            ))}
          </ul>
        </div>
      )}

      {comp.playstyle.trim() && <p className="comp-playstyle">{comp.playstyle}</p>}
      {comp.notes?.trim() && <p className="comp-notes">{comp.notes}</p>}
    </article>
  );
}

export function CompsPage() {
  const [meta, setMeta] = useState<MetaDetail | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      await connect();
      const detail = await getMetaComps();
      setMeta(detail);
    } catch {
      setError('Could not load the meta. Is the API running?');
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    void load();
  }, [load]);

  const comps = meta ? [...meta.comps].sort((a, b) => tierRank(a.tier) - tierRank(b.tier)) : [];

  return (
    <section className="comps-page">
      <div className="comps-header">
        <div className="comps-header-text">
          <h2>Current meta comps</h2>
          {meta ? (
            <p className="comps-subtitle">
              {meta.set} · patch {meta.patch} · sampled from the Riot Challenger ladder
            </p>
          ) : (
            <p className="comps-subtitle">Computed live from the top of the Riot ranked ladder.</p>
          )}
        </div>
        <button type="button" className="comps-refresh" onClick={load} disabled={loading}>
          {loading ? (
            <span className="btn-spinner">
              <span className="spinner" aria-hidden="true" />
              Loading…
            </span>
          ) : (
            'Refresh'
          )}
        </button>
      </div>

      {error && (
        <p className="error" role="alert">
          {error}
        </p>
      )}

      {loading && !meta && (
        <div className="comps-grid">
          {Array.from({ length: 6 }).map((_, i) => (
            <div key={i} className="comp-card comp-card-skeleton" aria-hidden="true" />
          ))}
        </div>
      )}

      {!loading && !error && comps.length === 0 && (
        <p className="comps-empty">
          No meta comps are available yet. The live sample may still be warming up — try Refresh in a moment.
        </p>
      )}

      {comps.length > 0 && (
        <div className="comps-grid">
          {comps.map((comp) => (
            <CompCard key={comp.name} comp={comp} />
          ))}
        </div>
      )}
    </section>
  );
}
