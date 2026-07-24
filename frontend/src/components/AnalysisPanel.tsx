import type { AnalysisResponse, PlacementTrend } from '../api';

function TrendBadge({ trend }: { trend: PlacementTrend }) {
  const label = String(trend);
  const symbol = label === 'Improving' ? '▲' : label === 'Declining' ? '▼' : '■';
  return (
    <span className={`trend trend-${label.toLowerCase()}`}>
      <span aria-hidden="true">{symbol}</span> {label}
    </span>
  );
}

function StatCard({ label, value }: { label: string; value: string }) {
  return (
    <div className="stat-card">
      <span className="stat-value">{value}</span>
      <span className="stat-label">{label}</span>
    </div>
  );
}

function PointList({ title, items, variant }: { title: string; items: string[]; variant: string }) {
  if (items.length === 0) {
    return null;
  }
  return (
    <div className={`point-list point-${variant}`}>
      <h4>{title}</h4>
      <ul>
        {items.map((item, i) => (
          <li key={i}>{item}</li>
        ))}
      </ul>
    </div>
  );
}

export function AnalysisPanel({ analysis }: { analysis: AnalysisResponse }) {
  const pct = (value: number) => `${Math.round(value * 100)}%`;

  return (
    <div className="insights">
      <h3 className="insights-title">
        <span className="insight-dot" aria-hidden="true" />
        Coaching Summary
        <span className="badge">{analysis.matchesAnalyzed} matches</span>
        <TrendBadge trend={analysis.trend} />
      </h3>

      <div className="stat-grid">
        <StatCard label="Avg placement" value={analysis.averagePlacement.toFixed(2)} />
        <StatCard label="Top 4 rate" value={pct(analysis.top4Rate)} />
        <StatCard label="Win rate" value={pct(analysis.winRate)} />
        <StatCard label="Avg level" value={analysis.averageLevel.toFixed(1)} />
      </div>

      {analysis.topComps.length > 0 && (
        <div className="comp-list">
          <h4>Most played comps</h4>
          <ul>
            {analysis.topComps.map((comp) => (
              <li key={comp.comp}>
                <span className="comp-name">{comp.comp}</span>
                <span className="comp-meta">
                  {comp.games} {comp.games === 1 ? 'game' : 'games'} · avg{' '}
                  {comp.averagePlacement.toFixed(2)}
                </span>
              </li>
            ))}
          </ul>
        </div>
      )}

      <div className="point-grid">
        <PointList title="Strengths" items={analysis.strengths} variant="strength" />
        <PointList title="Weaknesses" items={analysis.weaknesses} variant="weakness" />
        <PointList title="Suggestions" items={analysis.suggestions} variant="suggestion" />
      </div>

      {analysis.narrative && <p className="narrative">{analysis.narrative}</p>}
    </div>
  );
}
