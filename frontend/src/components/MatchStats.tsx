import type { TftMatchResult } from '../api';

function tone(placement: number): 'first' | 'top' | 'bottom' {
  if (placement === 1) return 'first';
  if (placement <= 4) return 'top';
  return 'bottom';
}

function pct(part: number, total: number): string {
  if (total === 0) return '0%';
  return `${Math.round((part / total) * 100)}%`;
}

// Bar with only its top corners rounded, sitting flush on the baseline.
function topRoundedRect(x: number, y: number, w: number, h: number, r: number): string {
  const rr = Math.min(r, w / 2, h);
  return `M ${x} ${y + h} L ${x} ${y + rr} Q ${x} ${y} ${x + rr} ${y} L ${x + w - rr} ${y} Q ${x + w} ${y} ${x + w} ${y + rr} L ${x + w} ${y + h} Z`;
}

// Smooth Catmull-Rom → cubic-bezier path (matches the rank climb chart treatment).
function smoothLine(pts: { x: number; y: number }[]): string {
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
}

// ---------- Distribution (1st..8th) bar chart ----------
function DistributionChart({ counts }: { counts: number[] }) {
  const max = Math.max(1, ...counts);
  const width = 320;
  const height = 150;
  const padBottom = 24;
  const padTop = 18;
  const slot = width / counts.length;
  const barW = slot * 0.56;
  const usableH = height - padBottom - padTop;
  const baselineY = padTop + usableH;

  return (
    <svg
      className="chart"
      viewBox={`0 0 ${width} ${height}`}
      role="img"
      aria-label="Placement distribution"
    >
      <line className="baseline" x1={6} x2={width - 6} y1={baselineY} y2={baselineY} />
      {counts.map((c, i) => {
        const placement = i + 1;
        const barH = c > 0 ? Math.max((c / max) * usableH, 4) : 0;
        const x = i * slot + (slot - barW) / 2;
        const y = baselineY - barH;
        return (
          <g key={placement} className={`bar ${tone(placement)}`}>
            {c > 0 && (
              <text className="bar-value" x={x + barW / 2} y={y - 5} textAnchor="middle">
                {c}
              </text>
            )}
            {c > 0 && <path d={topRoundedRect(x, y, barW, barH, 4)} />}
            <text
              className="axis-label"
              x={x + barW / 2}
              y={height - 7}
              textAnchor="middle"
            >
              {placement}
            </text>
          </g>
        );
      })}
    </svg>
  );
}

// ---------- Placement trend line chart ----------
function TrendChart({ placements }: { placements: number[] }) {
  const width = 320;
  const height = 150;
  const padX = 14;
  const padTop = 14;
  const padBottom = 18;
  const usableW = width - padX * 2;
  const usableH = height - padTop - padBottom;
  const n = placements.length;

  // y: placement 1 at top, 8 at bottom.
  const yFor = (p: number) => padTop + ((p - 1) / 7) * usableH;
  const xFor = (i: number) => (n <= 1 ? padX + usableW / 2 : padX + (i / (n - 1)) * usableW);

  const pts = placements.map((p, i) => ({ x: xFor(i), y: yFor(p) }));
  const linePath = smoothLine(pts);
  const baselineY = yFor(8);
  const areaPath =
    pts.length >= 2 ? `${linePath} L ${pts[pts.length - 1].x} ${baselineY} L ${pts[0].x} ${baselineY} Z` : '';

  return (
    <svg className="chart" viewBox={`0 0 ${width} ${height}`} role="img" aria-label="Placement over time">
      <defs>
        <linearGradient id="trend-area-fill" x1="0" y1="0" x2="0" y2="1">
          <stop offset="0%" stopColor="var(--accent)" stopOpacity="0.28" />
          <stop offset="100%" stopColor="var(--accent)" stopOpacity="0" />
        </linearGradient>
      </defs>
      {[1, 4, 8].map((p) => (
        <g key={p}>
          <line className="grid" x1={padX} x2={width - padX} y1={yFor(p)} y2={yFor(p)} />
          <text className="axis-label" x={2} y={yFor(p) + 3}>
            {p}
          </text>
        </g>
      ))}
      {areaPath && <path className="trend-area" d={areaPath} />}
      {n > 1 && <path className="trend-line" d={linePath} />}
      {pts.map((pt, i) => (
        <circle key={i} className={`trend-dot ${tone(placements[i])}`} cx={pt.x} cy={pt.y} r={3.5} />
      ))}
    </svg>
  );
}

export function MatchStats({ matches }: { matches: TftMatchResult[] }) {
  const games = matches.length;
  if (games === 0) return null;

  const placements = matches.map((m) => m.placement);
  const avg = placements.reduce((a, b) => a + b, 0) / games;
  const wins = placements.filter((p) => p === 1).length;
  const top4 = placements.filter((p) => p <= 4).length;

  const counts = Array.from({ length: 8 }, (_, i) => placements.filter((p) => p === i + 1).length);

  // Oldest -> newest, left to right.
  const chronological = [...placements].reverse();

  return (
    <section className="stats" aria-label="Performance summary">
      <div className="stat-cards">
        <div className="stat-card">
          <span className="stat-card-value">{games}</span>
          <span className="stat-card-label">Games</span>
        </div>
        <div className="stat-card">
          <span className="stat-card-value">{avg.toFixed(2)}</span>
          <span className="stat-card-label">Avg place</span>
        </div>
        <div className="stat-card">
          <span className="stat-card-value">{pct(top4, games)}</span>
          <span className="stat-card-label">Top 4 rate</span>
        </div>
        <div className="stat-card">
          <span className="stat-card-value">{pct(wins, games)}</span>
          <span className="stat-card-label">Win rate</span>
        </div>
      </div>

      <div className="charts">
        <div className="chart-panel">
          <h4 className="chart-title">Placement distribution</h4>
          <DistributionChart counts={counts} />
        </div>
        <div className="chart-panel">
          <h4 className="chart-title">Placement over time</h4>
          <TrendChart placements={chronological} />
        </div>
      </div>
    </section>
  );
}
