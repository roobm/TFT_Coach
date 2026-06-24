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
import { CalibrationOverlay } from './screen/CalibrationOverlay';
import { useScreenReader } from './screen/useScreenReader';
import type { OcrGameStatePartial } from './screen/parse';
import { decodeTeamCode } from './api';

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

  const [teamCode, setTeamCode] = useState('');
  const [decoding, setDecoding] = useState(false);
  const [decodeMsg, setDecodeMsg] = useState<string | null>(null);

  const [suggestion, setSuggestion] = useState<CoachingSuggestion | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  // Screen reader auto-fills the numeric fields from the player's own shared
  // window. It never submits — the player still presses "Get suggestion".
  function applySnapshot(partial: OcrGameStatePartial) {
    if (partial.stage !== undefined) setStage(partial.stage);
    if (partial.gold !== undefined) setGold(partial.gold);
    if (partial.level !== undefined) setLevel(partial.level);
    if (partial.health !== undefined) setHealth(partial.health);
  }

  const reader = useScreenReader(applySnapshot);
  const reading = reader.status === 'reading' || reader.status === 'starting';

  // Decode a pasted TFT Team Planner code into the board units list.
  async function handleDecodeTeamCode() {
    const code = teamCode.trim();
    if (!code) {
      return;
    }
    setDecoding(true);
    setDecodeMsg(null);
    try {
      const result = await decodeTeamCode(code);
      if (result.units.length === 0) {
        setDecodeMsg('No champions found in that code.');
        return;
      }
      setUnits(result.units.map((u) => u.name).join(', '));
      setDecodeMsg(`Loaded ${result.units.length} units from ${result.set}.`);
    } catch (err) {
      setDecodeMsg(err instanceof Error ? err.message : 'Could not decode that team code.');
    } finally {
      setDecoding(false);
    }
  }

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

      <div className="ocr-bar">
        <div className="ocr-bar-controls">
          {reading ? (
            <button type="button" className="ocr-btn ocr-stop" onClick={reader.stop}>
              Stop reading
            </button>
          ) : (
            <button type="button" className="ocr-btn ocr-start" onClick={() => void reader.start()}>
              Read my screen
            </button>
          )}
          <button type="button" className="ocr-btn ocr-cal" onClick={() => void reader.openCalibration()}>
            Calibrate
          </button>
          <button type="button" className="ocr-btn" onClick={() => void reader.readNow()}>
            Test once
          </button>
        </div>
        <div className="ocr-bar-status">
          {reader.status === 'starting' && <span className="ocr-hint">Starting…</span>}
          {reader.status === 'reading' && (
            <span className="ocr-hint ocr-live">
              <span className="status-dot" aria-hidden="true" />
              Reading your shared screen
              {reader.lastUpdated && (
                <em> · updated {new Date(reader.lastUpdated).toLocaleTimeString()}</em>
              )}
            </span>
          )}
          {reader.status === 'idle' && (
            <span className="ocr-hint ocr-muted">
              Auto-fills gold, level, stage &amp; health from a window you share. Advisory only.
            </span>
          )}
          {reader.error && <span className="ocr-hint ocr-error">{reader.error}</span>}
        </div>
      </div>

      {reader.lastReads.length > 0 && (
        <div className="ocr-diag" aria-live="polite">
          <span className="ocr-diag-title">Last read</span>
          {reader.lastReads.map((r) => {
            const accepted = r.confidence >= 60 && r.text.length > 0;
            return (
              <span
                key={r.field}
                className={`ocr-diag-chip ${accepted ? 'ok' : 'weak'}`}
                title={accepted ? 'Accepted' : 'Ignored (low confidence or empty)'}
              >
                {r.field}: <strong>{r.text || '—'}</strong>
                <em>{Math.round(r.confidence)}%</em>
              </span>
            );
          })}
        </div>
      )}

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
              <div className="teamcode-row">
                <input
                  type="text"
                  className="teamcode-input"
                  value={teamCode}
                  onChange={(e) => setTeamCode(e.target.value)}
                  placeholder="Paste TFT team code, e.g. 0201d00d…TFTSet17"
                />
                <button
                  type="button"
                  className="teamcode-btn"
                  onClick={() => void handleDecodeTeamCode()}
                  disabled={decoding || teamCode.trim().length === 0}
                >
                  {decoding ? 'Decoding…' : 'Decode'}
                </button>
              </div>
              {decodeMsg && <span className="teamcode-msg">{decodeMsg}</span>}
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

      {reader.calibrationFrame && (
        <CalibrationOverlay
          frameDataUrl={reader.calibrationFrame}
          initialRegions={reader.regions}
          onSave={reader.saveCalibration}
          onCancel={reader.cancelCalibration}
        />
      )}
    </>
  );
}
