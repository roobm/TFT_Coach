import type { RankedEntry, RankSnapshot, TftMatchResult } from '../api';

// Ranked TFT queue id. Other modes (normal, hyper roll, double up) are excluded
// from the climb estimate so they don't distort the curve.
const RANKED_TFT_QUEUE = 1100;

const TIERS = [
  'IRON',
  'BRONZE',
  'SILVER',
  'GOLD',
  'PLATINUM',
  'EMERALD',
  'DIAMOND',
  'MASTER',
  'GRANDMASTER',
  'CHALLENGER',
];

const DIVISION_OFFSET: Record<string, number> = { IV: 0, III: 100, II: 200, I: 300 };
const MASTER_FLOOR = TIERS.indexOf('MASTER') * 400; // 2800

// Rough average LP swing per placement in Ranked TFT (1st = +, 8th = −).
// Used only for the *estimated* climb; Riot's API does not expose real per-game LP.
const LP_DELTA: Record<number, number> = {
  1: 40,
  2: 30,
  3: 20,
  4: 10,
  5: -10,
  6: -20,
  7: -30,
  8: -40,
};

function titleCase(value: string): string {
  return value.charAt(0) + value.slice(1).toLowerCase();
}

// Convert a tier/division/LP into a single absolute ladder value so points are
// comparable on one axis. Master+ share one pool above Diamond I.
function absoluteLp(tier: string | null, division: string | null, leaguePoints: number): number | null {
  if (!tier) return null;
  const index = TIERS.indexOf(tier);
  if (index < 0) return null;
  if (index >= TIERS.indexOf('MASTER')) {
    return MASTER_FLOOR + leaguePoints;
  }
  return index * 400 + (division ? DIVISION_OFFSET[division] ?? 0 : 0) + leaguePoints;
}

function lpToLabel(lp: number): string {
  if (lp >= MASTER_FLOOR) {
    return `Master ${lp - MASTER_FLOOR} LP`;
  }
  const index = Math.min(TIERS.indexOf('DIAMOND'), Math.floor(lp / 400));
  const within = lp - index * 400;
  const division = ['IV', 'III', 'II', 'I'][Math.min(3, Math.floor(within / 100))];
  return `${titleCase(TIERS[index])} ${division}`;
}

function lpToRankWithPoints(lp: number): string {
  if (lp >= MASTER_FLOOR) {
    return `Master ${lp - MASTER_FLOOR} LP`;
  }

  const clamped = Math.max(0, lp);
  const tierIndex = Math.min(TIERS.indexOf('DIAMOND'), Math.floor(clamped / 400));
  const withinTier = clamped - tierIndex * 400;
  const divisionIndex = Math.min(3, Math.floor(withinTier / 100));
  const division = ['IV', 'III', 'II', 'I'][divisionIndex];
  const leaguePoints = withinTier - divisionIndex * 100;
  return `${titleCase(TIERS[tierIndex])} ${division} ${leaguePoints} LP`;
}

interface Point {
  t: number;
  lp: number;
}

// Walk ranked matches oldest→newest, anchoring the final point to the player's
// current absolute LP and reconstructing earlier values from placement deltas.
function estimatedSeries(matches: TftMatchResult[], current: RankedEntry | undefined): Point[] {
  if (!current) return [];
  const anchor = absoluteLp(current.tier, current.division, current.leaguePoints);
  if (anchor === null) return [];

  const ranked = matches
    .filter((m) => m.queueId === RANKED_TFT_QUEUE)
    .slice()
    .sort((a, b) => new Date(a.playedAt).getTime() - new Date(b.playedAt).getTime());
  if (ranked.length === 0) return [];

  const deltas = ranked.map((m) => LP_DELTA[m.placement] ?? 0);
  // lpAfter[i] = LP after game i; lpAfter[last] = anchor (current).
  const lpAfter: number[] = new Array(ranked.length);
  lpAfter[ranked.length - 1] = anchor;
  for (let i = ranked.length - 2; i >= 0; i--) {
    lpAfter[i] = lpAfter[i + 1] - deltas[i + 1];
  }

  return ranked.map((m, i) => ({ t: new Date(m.playedAt).getTime(), lp: Math.max(0, lpAfter[i]) }));
}

function realSeries(history: RankSnapshot[]): Point[] {
  return history
    .filter((s) => s.queueType === 'RANKED_TFT')
    .map((s) => ({ t: new Date(s.recordedAt).getTime(), lp: absoluteLp(s.tier, s.division, s.leaguePoints) }))
    .filter((p): p is Point => p.lp !== null)
    .sort((a, b) => a.t - b.t);
}

export function RankClimbChart({
  matches,
  current,
  history,
}: {
  matches: TftMatchResult[];
  current: RankedEntry | undefined;
  history: RankSnapshot[];
}) {
  const estimate = estimatedSeries(matches, current);
  const real = realSeries(history);

  if (estimate.length < 2 && real.length < 2) {
    return (
      <section className="climb" aria-label="Rank climb">
        <h3 className="climb-title">Rank climb</h3>
        <p className="climb-empty">
          Not enough ranked data yet. Play Ranked TFT games and re-search to chart your climb — accurate
          history builds up each time you check your rank.
        </p>
      </section>
    );
  }

  const width = 660;
  const height = 240;
  const padL = 64;
  const padR = 16;
  const padTop = 20;
  const padBottom = 24;
  const usableW = width - padL - padR;
  const usableH = height - padTop - padBottom;
  const baselineY = padTop + usableH;

  const allPoints = [...estimate, ...real];
  const rawMin = Math.min(...allPoints.map((p) => p.lp));
  const rawMax = Math.max(...allPoints.map((p) => p.lp));
  // Tight padding so the line fills the plot area rather than floating in an
  // empty band. Small absolute floor keeps a flat run off the very edges.
  const pad = Math.max(12, (rawMax - rawMin) * 0.08);
  const minLp = Math.max(0, rawMin - pad);
  const maxLp = rawMax + pad;

  // Space points EVENLY by game index, not by raw timestamp — games clustered in
  // time would otherwise collapse into near-vertical jitter. Each series gets its
  // own even spread across the full plot width.
  const xForIndex = (i: number, count: number) =>
    count <= 1 ? padL + usableW / 2 : padL + (i / (count - 1)) * usableW;
  const yFor = (lp: number) => padTop + (1 - (lp - minLp) / (maxLp - minLp || 1)) * usableH;

  const estPts = estimate.map((p, i) => ({ x: xForIndex(i, estimate.length), y: yFor(p.lp) }));
  const realPts = real.map((p, i) => ({ x: xForIndex(i, real.length), y: yFor(p.lp) }));

  // Smooth Catmull-Rom → cubic-bezier path for a polished, non-jagged climb.
  const smoothLine = (pts: { x: number; y: number }[]): string => {
    if (pts.length === 0) return '';
    if (pts.length === 1) return `M ${pts[0].x} ${pts[0].y}`;
    const tension = 0.18;
    let d = `M ${pts[0].x} ${pts[0].y}`;
    for (let i = 0; i < pts.length - 1; i++) {
      const p0 = pts[i - 1] ?? pts[i];
      const p1 = pts[i];
      const p2 = pts[i + 1];
      const p3 = pts[i + 2] ?? p2;
      const c1x = p1.x + (p2.x - p0.x) * tension;
      const c1y = p1.y + (p2.y - p0.y) * tension;
      const c2x = p2.x - (p3.x - p1.x) * tension;
      const c2y = p2.y - (p3.y - p1.y) * tension;
      d += ` C ${c1x} ${c1y} ${c2x} ${c2y} ${p2.x} ${p2.y}`;
    }
    return d;
  };

  const estLinePath = smoothLine(estPts);
  const estAreaPath =
    estPts.length >= 2
      ? `${estLinePath} L ${estPts[estPts.length - 1].x} ${baselineY} L ${estPts[0].x} ${baselineY} Z`
      : '';
  const realLinePath = smoothLine(realPts);

  // Division gridlines (every 100 LP) within the visible range, e.g. "Diamond II".
  // Above Master there are no divisions, so those lines are labelled by LP.
  const bandLabel = (lp: number): string => {
    if (lp >= MASTER_FLOOR) {
      return lp === MASTER_FLOOR ? 'Master' : `Master +${lp - MASTER_FLOOR}`;
    }
    const index = Math.floor(lp / 400);
    const within = lp - index * 400;
    const division = ['IV', 'III', 'II', 'I'][Math.min(3, Math.floor(within / 100))];
    return `${titleCase(TIERS[index])} ${division}`;
  };

  const lpRange = maxLp - minLp;
  // Keep the chart readable: step up from 100 LP (one per division) when the
  // visible span is very large so labels don't overlap.
  const bandStep = lpRange > 2000 ? 400 : lpRange > 1000 ? 200 : 100;
  const bands: { lp: number; label: string }[] = [];
  for (let lp = Math.ceil(minLp / bandStep) * bandStep; lp <= maxLp; lp += bandStep) {
    bands.push({ lp, label: bandLabel(lp) });
  }

  const netEstimate = estimate.length >= 2 ? estimate[estimate.length - 1].lp - estimate[0].lp : 0;
  const peakLp = Math.max(...allPoints.map((point) => point.lp));
  const peakLabel = lpToRankWithPoints(Math.round(peakLp));

  // Current-rank callout pill, offset clear of the line so it never overlaps the
  // path or end marker. A climbing line ends at the top, so the pill goes above;
  // a declining line ends at the bottom, so it goes below.
  const endPt = estPts.length >= 1 ? estPts[estPts.length - 1] : null;
  const endLabel = estimate.length >= 1 ? lpToLabel(estimate[estimate.length - 1].lp) : '';
  const climbing =
    estimate.length >= 2 ? estimate[estimate.length - 1].lp >= estimate[estimate.length - 2].lp : true;
  const pillH = 22;
  const pillW = Math.max(64, endLabel.length * 6.7 + 18);
  let pillX = endPt ? endPt.x - pillW + 6 : 0;
  pillX = Math.max(padL, Math.min(width - padR - pillW, pillX));
  let pillY = endPt ? (climbing ? endPt.y - 14 - pillH : endPt.y + 14) : 0;
  pillY = Math.max(padTop, Math.min(baselineY - pillH, pillY));
  const pillCx = pillX + pillW / 2;
  const pillCy = pillY + pillH / 2;

  return (
    <section className="climb" aria-label="Rank climb">
      <div className="climb-head">
        <h3 className="climb-title">Rank climb</h3>
        <div className="climb-metrics">
          <span className="climb-peak">Peak: {peakLabel}</span>
          {estimate.length >= 2 && (
            <span className={`climb-net ${netEstimate >= 0 ? 'up' : 'down'}`}>
              {netEstimate >= 0 ? '▲' : '▼'} {Math.abs(Math.round(netEstimate))} LP est. over {estimate.length} ranked
            </span>
          )}
        </div>
      </div>

      <svg className="climb-chart" viewBox={`0 0 ${width} ${height}`} role="img" aria-label="Estimated rank progression">
        <defs>
          <linearGradient id="climb-area-fill" x1="0" y1="0" x2="0" y2="1">
            <stop offset="0%" stopColor="var(--accent)" stopOpacity="0.32" />
            <stop offset="100%" stopColor="var(--accent)" stopOpacity="0" />
          </linearGradient>
        </defs>

        {bands.map((b) => (
          <g key={b.lp}>
            <line className="climb-grid" x1={padL} x2={width - padR} y1={yFor(b.lp)} y2={yFor(b.lp)} />
            <text className="climb-band-label" x={6} y={yFor(b.lp) + 4}>
              {b.label}
            </text>
          </g>
        ))}

        {estAreaPath && <path className="climb-area" d={estAreaPath} />}
        {estPts.length >= 2 && <path className="climb-line climb-estimate" d={estLinePath} />}
        {realPts.length >= 2 && <path className="climb-line climb-real" d={realLinePath} />}
        {realPts.map((p, i) => (
          <circle key={i} className="climb-dot" cx={p.x} cy={p.y} r={3.5} />
        ))}

        {/* Current LP callout — connector + marker + pill, kept clear of the line */}
        {endPt && (
          <g>
            <line className="climb-connector" x1={endPt.x} y1={endPt.y} x2={pillCx} y2={pillCy} />
            <circle className="climb-end-dot" cx={endPt.x} cy={endPt.y} r={4.5} />
            <rect className="climb-badge-bg" x={pillX} y={pillY} width={pillW} height={pillH} rx={11} />
            <text className="climb-badge-text" x={pillCx} y={pillCy + 4} textAnchor="middle">
              {endLabel}
            </text>
          </g>
        )}
      </svg>

      <div className="climb-legend">
        {estimate.length >= 2 && (
          <span className="climb-legend-item">
            <span className="climb-swatch estimate" aria-hidden="true" /> Estimated from placements
          </span>
        )}
        {real.length >= 1 && (
          <span className="climb-legend-item">
            <span className="climb-swatch real" aria-hidden="true" /> Recorded snapshots
          </span>
        )}
      </div>
    </section>
  );
}
