import { useEffect, useState } from 'react';
import {
  connect,
  disconnect,
  getMeta,
  requestSuggestion,
  type CoachingSuggestion,
  type GameState,
  type MetaInfo,
} from './coachHub';

// One unit/item per line or comma-separated — split into a clean list.
function toList(value: string): string[] {
  return value
    .split(/[\n,]/)
    .map((item) => item.trim())
    .filter((item) => item.length > 0);
}

const ONLINE_KEY = 'tftCoach.online';

export function LiveCoachPage() {
  const [online, setOnline] = useState(() => localStorage.getItem(ONLINE_KEY) === 'true');
  const [connecting, setConnecting] = useState(false);
  const [meta, setMeta] = useState<MetaInfo | null>(null);

  const [stage, setStage] = useState('');
  const [gold, setGold] = useState(0);
  const [level, setLevel] = useState(8);
  const [health, setHealth] = useState(100);
  const [streak, setStreak] = useState(0);
  const [units, setUnits] = useState('');
  const [items, setItems] = useState('');
  const [notes, setNotes] = useState('');

  const [suggestion, setSuggestion] = useState<CoachingSuggestion | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  // Open/close the live connection as the coach is toggled, and load the meta
  // the coach is grounded on once connected.
  useEffect(() => {
    let cancelled = false;
    localStorage.setItem(ONLINE_KEY, String(online));

    if (!online) {
      setMeta(null);
      disconnect().catch(() => undefined);
      return;
    }

    setConnecting(true);
    setError(null);
    (async () => {
      try {
        await connect();
        const info = await getMeta();
        if (!cancelled) {
          setMeta(info);
        }
      } catch {
        if (!cancelled) {
          setError('Could not connect to the coach. Is the API running?');
          setOnline(false);
        }
      } finally {
        if (!cancelled) {
          setConnecting(false);
        }
      }
    })();

    return () => {
      cancelled = true;
    };
  }, [online]);

  async function handleSubmit(event: React.FormEvent) {
    event.preventDefault();
    if (!online) {
      return;
    }
    setLoading(true);
    setError(null);
    setSuggestion(null);

    const state: GameState = {
      stage: stage.trim(),
      gold,
      level,
      health,
      streak,
      units: toList(units),
      items: toList(items),
      notes: notes.trim() || undefined,
    };

    try {
      const result = await requestSuggestion(state);
      setSuggestion(result);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to get a suggestion.');
    } finally {
      setLoading(false);
    }
  }

  return (
    <>
      <div className="coach-statusbar">
        <button
          type="button"
          role="switch"
          aria-checked={online}
          className={`coach-toggle ${online ? 'on' : 'off'}`}
          onClick={() => setOnline((v) => !v)}
        >
          <span className="coach-toggle-track">
            <span className="coach-toggle-thumb" />
          </span>
          <span className="coach-toggle-label">
            {online ? 'Coach online' : 'Coach offline'}
          </span>
        </button>

        <div className="coach-status-meta">
          <span className={`status-pill ${online ? 'online' : 'offline'}`}>
            <span className="status-dot" aria-hidden="true" />
            {connecting ? 'Connecting…' : online ? 'Live' : 'Idle'}
          </span>
          {meta && (
            <span
              className="meta-badge"
              title={meta.comps.length > 0 ? `Top comps: ${meta.comps.join(', ')}` : undefined}
            >
              Meta: {meta.set} · patch {meta.patch}
            </span>
          )}
        </div>
      </div>

      <form className="coach-form" onSubmit={handleSubmit}>
        <fieldset className="coach-fieldset" disabled={!online}>
          <div className="coach-grid">
            <label className="coach-field">
              <span>Stage</span>
              <input
                type="text"
                value={stage}
                onChange={(e) => setStage(e.target.value)}
                placeholder="e.g. 3-2"
              />
            </label>
            <label className="coach-field">
              <span>Gold</span>
              <input type="number" min={0} value={gold} onChange={(e) => setGold(Number(e.target.value))} />
            </label>
            <label className="coach-field">
              <span>Level</span>
              <input type="number" min={1} max={11} value={level} onChange={(e) => setLevel(Number(e.target.value))} />
            </label>
            <label className="coach-field">
              <span>Health</span>
              <input type="number" min={0} max={100} value={health} onChange={(e) => setHealth(Number(e.target.value))} />
            </label>
            <label className="coach-field">
              <span>Streak (+win / −loss)</span>
              <input type="number" value={streak} onChange={(e) => setStreak(Number(e.target.value))} />
            </label>
          </div>

          <div className="coach-grid coach-grid-wide">
            <label className="coach-field">
              <span>Board units</span>
              <textarea
                rows={3}
                value={units}
                onChange={(e) => setUnits(e.target.value)}
                placeholder="One per line, e.g. Aphelios ★★, Jinx, Lux"
              />
            </label>
            <label className="coach-field">
              <span>Items / components</span>
              <textarea
                rows={3}
                value={items}
                onChange={(e) => setItems(e.target.value)}
                placeholder="e.g. Guinsoo, Tear, Recurve Bow"
              />
            </label>
          </div>

          <label className="coach-field">
            <span>Notes (optional)</span>
            <textarea
              rows={2}
              value={notes}
              onChange={(e) => setNotes(e.target.value)}
              placeholder="Anything else — what you're contesting, augments, etc."
            />
          </label>

          <div className="coach-actions">
            <button type="submit" disabled={loading}>
              {loading ? (
                <span className="btn-spinner">
                  <span className="spinner" aria-hidden="true" />
                  Thinking…
                </span>
              ) : (
                'Get suggestion'
              )}
            </button>
          </div>
        </fieldset>
      </form>

      {error && (
        <p className="error" role="alert">
          {error}
        </p>
      )}

      {suggestion && (
        <section className="suggestion-card" aria-live="polite">
          <span className={`suggestion-tag ${suggestion.category.toLowerCase()}`}>
            {suggestion.category.toUpperCase()}
          </span>
          <p className="suggestion-text">{suggestion.text}</p>
        </section>
      )}

      {!suggestion && !error && !loading && (
        <div className="empty">
          <h3>Live coach</h3>
          <p>
            {online
              ? 'Enter your current board and get one high-impact suggestion for this moment.'
              : 'Toggle the coach online when you load into a game to start getting live suggestions.'}
          </p>
        </div>
      )}
    </>
  );
}
