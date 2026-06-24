import { useState } from 'react';
import { requestSuggestion, type CoachingSuggestion, type GameState } from './coachHub';

// One unit/item per line or comma-separated — split into a clean list.
function toList(value: string): string[] {
  return value
    .split(/[\n,]/)
    .map((item) => item.trim())
    .filter((item) => item.length > 0);
}

export function LiveCoachPage() {
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

  async function handleSubmit(event: React.FormEvent) {
    event.preventDefault();
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
      <form className="coach-form" onSubmit={handleSubmit}>
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
          <p>Enter your current board and get one high-impact suggestion for this moment.</p>
        </div>
      )}
    </>
  );
}
