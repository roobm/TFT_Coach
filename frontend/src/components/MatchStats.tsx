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

// ---------- Distribution (1st..8th) bar chart ----------
function DistributionChart({ counts }: { counts: number[] }) {
  const max = Math.max(1, ...counts);
  const width = 320;
  const height = 150;
  const padBottom = 24;
  const padTop = 16;
  const slot = width / counts.length;
  const barW = slot * 0.56;
  const usableH = height - padBottom - padTop;

  return (
    <svg
      className="chart"
      viewBox={`0 0 ${width} ${height}`}
      role="img"
      aria-label="Placement distribution"
    >
      {counts.map((c, i) => {
        const placement = i + 1;
        const barH = (c / max) * usableH;
        const x = i * slot + (slot - barW) / 2;
        const y = padTop + (usableH - barH);
        return (
          <g key={placement} className={`bar ${tone(placement)}`}>
            {c > 0 && (
              <text className="bar-value" x={x + barW / 2} y={y - 5} textAnchor="middle">
                {c}
              </text>
            )}
            <rect x={x} y={y} width={barW} height={Math.max(barH, c > 0 ? 2 : 0)} rx={3} />
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

  const points = placements.map((p, i) => `${xFor(i)},${yFor(p)}`).join(' ');

  return (
    <svg className="chart" viewBox={`0 0 ${width} ${height}`} role="img" aria-label="Placement over time">
      {[1, 4, 8].map((p) => (
        <g key={p}>
          <line className="grid" x1={padX} x2={width - padX} y1={yFor(p)} y2={yFor(p)} />
          <text className="axis-label" x={2} y={yFor(p) + 3}>
            {p}
          </text>
        </g>
      ))}
      {n > 1 && <polyline className="trend-line" points={points} />}
      {placements.map((p, i) => (
        <circle key={i} className={`trend-dot ${tone(p)}`} cx={xFor(i)} cy={yFor(p)} r={3.5} />
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
