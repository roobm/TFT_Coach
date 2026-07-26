import { useState } from 'react';
import { useOutletContext } from 'react-router-dom';
import { fetchMatchHistory, fetchRanks } from './api';
import type { AppContext, HistoryState } from './Layout';
import { RankClimbChart } from './components/RankClimbChart';

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
const MASTER_FLOOR = TIERS.indexOf('MASTER') * 400;

function absoluteLp(tier: string, division: string | null, leaguePoints: number): number {
  const tierIndex = TIERS.indexOf(tier);
  if (tierIndex < 0) {
    return Math.max(0, leaguePoints);
  }

  if (tierIndex >= TIERS.indexOf('MASTER')) {
    return MASTER_FLOOR + Math.max(0, leaguePoints);
  }

  return tierIndex * 400 + (division ? DIVISION_OFFSET[division] ?? 0 : 0) + Math.max(0, leaguePoints);
}

function filterMatchesForSeason(
  matches: HistoryState['allSeasonMatches'],
  currentSet: number,
  season: number,
) {
  if (season <= 0) {
    return matches.filter((match) =>
      currentSet <= 0 ? true : match.setNumber === 0 || match.setNumber === currentSet,
    );
  }

  return matches.filter((match) => match.setNumber === season);
}

function buildSeasonOptions(
  currentSet: number,
  allSeasonMatches: HistoryState['allSeasonMatches'],
  bestBySeason: HistoryState['bestBySeason'],
) {
  const knownSets = new Set<number>();
  for (const match of allSeasonMatches) {
    if (match.setNumber > 0) {
      knownSets.add(match.setNumber);
    }
  }

  for (const seasonRank of bestBySeason) {
    if (seasonRank.setNumber > 0) {
      knownSets.add(seasonRank.setNumber);
    }
  }

  const highestKnownSet = knownSets.size > 0 ? Math.max(...knownSets) : 0;
  const maxSet = Math.max(currentSet, highestKnownSet);

  const options = [{ value: 0, label: currentSet > 0 ? `This season (Set ${currentSet})` : 'This season' }];
  for (let setNumber = maxSet; setNumber >= 1; setNumber--) {
    if (setNumber !== currentSet) {
      options.push({ value: setNumber, label: `Set ${setNumber}` });
    }
  }

  return options;
}

function SearchIcon() {
  return (
    <svg className="search-icon" viewBox="0 0 24 24" fill="none" aria-hidden="true">
      <circle cx="11" cy="11" r="7" stroke="currentColor" strokeWidth="2" />
      <path d="m20 20-3.5-3.5" stroke="currentColor" strokeWidth="2" strokeLinecap="round" />
    </svg>
  );
}

function ErrorIcon() {
  return (
    <svg viewBox="0 0 24 24" width="18" height="18" fill="none" aria-hidden="true">
      <circle cx="12" cy="12" r="9" stroke="currentColor" strokeWidth="2" />
      <path d="M12 7v6" stroke="currentColor" strokeWidth="2" strokeLinecap="round" />
      <circle cx="12" cy="16.5" r="1.1" fill="currentColor" />
    </svg>
  );
}

function EmptyIcon() {
  return (
    <svg className="empty-icon" viewBox="0 0 24 24" fill="none" aria-hidden="true">
      <rect x="3" y="4" width="18" height="16" rx="2" stroke="currentColor" strokeWidth="1.6" />
      <path d="M7 9h10M7 13h6" stroke="currentColor" strokeWidth="1.6" strokeLinecap="round" />
    </svg>
  );
}

function titleCase(value: string): string {
  return value.charAt(0) + value.slice(1).toLowerCase();
}

function formatRecordedAt(iso: string): string {
  const date = new Date(iso);
  if (Number.isNaN(date.getTime())) return 'Date unavailable';
  return date.toLocaleDateString(undefined, { month: 'short', day: 'numeric', year: 'numeric' });
}

function lpToAxisLabel(lp: number): string {
  if (lp >= MASTER_FLOOR) return `Master +${Math.round(lp - MASTER_FLOOR)}`;
  const clamped = Math.max(0, lp);
  const tierIndex = Math.min(TIERS.indexOf('DIAMOND'), Math.floor(clamped / 400));
  const withinTier = clamped - tierIndex * 400;
  const division = ['IV', 'III', 'II', 'I'][Math.min(3, Math.floor(withinTier / 100))];
  return `${titleCase(TIERS[tierIndex])} ${division}`;
}

export function HistoryPage() {
  const { riotId, setRiotId, historyState, setHistoryState } = useOutletContext<AppContext>();
  const { season, currentSet, allSeasonMatches, matches, ranks, history, bestBySeason, searched } = historyState;
  const update = (patch: Partial<HistoryState>) => setHistoryState((s) => ({ ...s, ...patch }));
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const formatBestRank = (tier: string, division: string | null, leaguePoints: number) => {
    const label = tier.charAt(0) + tier.slice(1).toLowerCase();
    if (tier === 'MASTER' || tier === 'GRANDMASTER' || tier === 'CHALLENGER') {
      return `${label} ${leaguePoints} LP`;
    }

    return division ? `${label} ${division} ${leaguePoints} LP` : `${label} ${leaguePoints} LP`;
  };

  async function loadHistory() {
    const trimmed = riotId.trim();
    if (trimmed.length === 0) {
      return;
    }

    setLoading(true);
    setError(null);
    update({ searched: true, allSeasonMatches: [], matches: [], ranks: null, history: [], bestBySeason: [] });

    // Backfill all seasons once so the history season picker can switch instantly.
    const allSeasonsResult = await fetchMatchHistory(trimmed, 0, -1).catch(() => null);
    if (allSeasonsResult !== null) {
      const resolvedCurrentSet = allSeasonsResult.currentSet;
      const selectedSeasonMatches = filterMatchesForSeason(allSeasonsResult.matches, resolvedCurrentSet, season);
      update({
        currentSet: resolvedCurrentSet,
        allSeasonMatches: allSeasonsResult.matches,
        matches: selectedSeasonMatches,
      });
    }

    try {
      const rankResult = await fetchRanks(trimmed);
      update({
        ranks: rankResult.entries,
        history: rankResult.history,
        bestBySeason: rankResult.bestBySeason,
      });
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to load rank history.');
    } finally {
      setLoading(false);
    }
  }

  async function handleSubmit(event: React.FormEvent) {
    event.preventDefault();
    await loadHistory();
  }

  function handleSeasonChange(nextSeason: number) {
    update({
      season: nextSeason,
      matches: filterMatchesForSeason(allSeasonMatches, currentSet, nextSeason),
    });
  }

  const seasonOptions = buildSeasonOptions(currentSet, allSeasonMatches, bestBySeason);
  const ranked = ranks?.find((entry) => entry.queueType === 'RANKED_TFT');
  const chartHistory = season === 0 ? history : [];
  const selectedSet = season > 0 ? season : currentSet;
  const selectedSeasonOption = seasonOptions.find((option) => option.value === season);
  const selectedSeasonLabel = selectedSeasonOption?.label ?? (selectedSet > 0 ? `Set ${selectedSet}` : 'This season');
  const selectedSeasonPeak =
    selectedSet > 0 ? bestBySeason.find((seasonBest) => seasonBest.setNumber === selectedSet) ?? null : null;
  const highestSetFromOptions = seasonOptions
    .map((option) => option.value)
    .filter((value) => value > 0)
    .reduce((max, value) => Math.max(max, value), 0);
  const allChartSets: number[] = [];
  for (let setNumber = 1; setNumber <= highestSetFromOptions; setNumber++) {
    allChartSets.push(setNumber);
  }

  const peakBySet = new Map(
    bestBySeason.map((rank) => [
      rank.setNumber,
      {
        ...rank,
        score: absoluteLp(rank.tier, rank.division, rank.leaguePoints),
      },
    ]),
  );
  const knownSeasonPeaks = allChartSets
    .map((setNumber) => peakBySet.get(setNumber))
    .filter((peak): peak is NonNullable<typeof peak> => peak !== undefined);
  const seasonPeaks = allChartSets.map((setNumber) => ({
    setNumber,
    peak: peakBySet.get(setNumber) ?? null,
  }));
  const latestSeasonPeaks = [...seasonPeaks].sort((a, b) => b.setNumber - a.setNumber);
  const topSeasonPeak =
    knownSeasonPeaks.length > 0
      ? knownSeasonPeaks.reduce((best, current) => (current.score > best.score ? current : best), knownSeasonPeaks[0])
      : null;
  const chartWidth = 680;
  const chartHeight = 240;
  const chartPadL = 28;
  const chartPadR = 104;
  const chartPadTop = 20;
  const chartPadBottom = 32;
  const chartUsableW = chartWidth - chartPadL - chartPadR;
  const chartUsableH = chartHeight - chartPadTop - chartPadBottom;
  const minScore = knownSeasonPeaks.length > 0 ? Math.min(...knownSeasonPeaks.map((item) => item.score)) : 0;
  const maxScore = knownSeasonPeaks.length > 0 ? Math.max(...knownSeasonPeaks.map((item) => item.score)) : 0;
  const scorePad = Math.max(20, (maxScore - minScore) * 0.12);
  const yForScore = (score: number) =>
    chartPadTop + (1 - (score - (minScore - scorePad)) / ((maxScore + scorePad) - (minScore - scorePad) || 1)) * chartUsableH;
  const xForIndex = (index: number) =>
    seasonPeaks.length <= 1 ? chartPadL + chartUsableW / 2 : chartPadL + (index / (seasonPeaks.length - 1)) * chartUsableW;
  const chartPoints = seasonPeaks.map((item, index) => ({
    x: xForIndex(index),
    y: item.peak ? yForScore(item.peak.score) : null,
    setNumber: item.setNumber,
    label: item.peak ? formatBestRank(item.peak.tier, item.peak.division, item.peak.leaguePoints) : 'No peak recorded',
    hasData: item.peak !== null,
  }));
  const chartTicks = Array.from({ length: 5 }, (_, index) => {
    const ratio = index / 4;
    const score = maxScore + scorePad - ratio * ((maxScore + scorePad) - (minScore - scorePad));
    return { y: yForScore(score), label: lpToAxisLabel(Math.round(score)) };
  });
  let peaksPath = '';
  let hasOpenSegment = false;
  for (const point of chartPoints) {
    if (point.y === null) {
      hasOpenSegment = false;
      continue;
    }

    if (!hasOpenSegment) {
      peaksPath = `${peaksPath} M ${point.x} ${point.y}`.trim();
      hasOpenSegment = true;
      continue;
    }

    peaksPath = `${peaksPath} L ${point.x} ${point.y}`;
  }
  const hasData = (ranked && matches.length > 0) || history.length > 0 || bestBySeason.length > 0;

  return (
    <>
      <form className="search" onSubmit={handleSubmit}>
        <div className="search-field">
          <SearchIcon />
          <input
            type="text"
            value={riotId}
            onChange={(e) => setRiotId(e.target.value)}
            placeholder="Riot ID, e.g. Faker#EUW"
            aria-label="Riot ID"
          />
        </div>
        <select
          className="count-select"
          value={season}
          onChange={(e) => handleSeasonChange(Number(e.target.value))}
          aria-label="Season"
          disabled={loading}
        >
          {seasonOptions.map(({ value, label }) => (
            <option key={value} value={value}>
              {label}
            </option>
          ))}
        </select>
        <button type="submit" disabled={loading}>
          {loading ? (
            <span className="btn-spinner">
              <span className="spinner" aria-hidden="true" />
              Loading…
            </span>
          ) : (
            'Load history'
          )}
        </button>
      </form>

      {error && (
        <p className="error" role="alert">
          <ErrorIcon />
          {error}
        </p>
      )}

      {loading && (
        <section className="history-loading" aria-busy="true" aria-label="Loading rank history">
          <article className="skeleton-card history-loading-card">
            <div className="sk sk-line history-sk-title" />
            <div className="history-sk-grid">
              <div className="sk history-sk-stat" />
              <div className="sk history-sk-stat" />
              <div className="sk history-sk-stat" />
            </div>
            <div className="sk history-sk-chart" />
          </article>
        </section>
      )}

      {!loading && hasData && (
        <section className="results history-results" aria-live="polite">
          <header className="history-header">
            <div>
              <p className="history-kicker">History overview</p>
              <h2 className="history-title">Rank progression and seasonal peaks</h2>
            </div>
            <span className="history-season-chip">{selectedSeasonLabel}</span>
          </header>

          <div className="history-summary-grid">
            <article className="history-stat-card">
              <span className="history-stat-label">Selected context</span>
              <p className="history-stat-value">{selectedSeasonLabel}</p>
              <p className="history-stat-note">{matches.length} ranked matches loaded</p>
            </article>
            <article className="history-stat-card history-stat-card-peak">
              <span className="history-stat-label">Selected season peak</span>
              <p className="history-stat-value">
                {selectedSeasonPeak
                  ? formatBestRank(selectedSeasonPeak.tier, selectedSeasonPeak.division, selectedSeasonPeak.leaguePoints)
                  : 'No peak recorded'}
              </p>
              <p className="history-stat-note">
                {selectedSeasonPeak ? `Set ${selectedSeasonPeak.setNumber}` : 'Keep playing ranked to record a peak'}
              </p>
            </article>
            <article className="history-stat-card">
              <span className="history-stat-label">Best season overall</span>
              <p className="history-stat-value">
                {topSeasonPeak
                  ? `Set ${topSeasonPeak.setNumber} · ${formatBestRank(
                      topSeasonPeak.tier,
                      topSeasonPeak.division,
                      topSeasonPeak.leaguePoints,
                    )}`
                  : 'No data yet'}
              </p>
              <p className="history-stat-note">{seasonPeaks.length} seasons compared</p>
            </article>
          </div>

          <RankClimbChart matches={matches} current={ranked} history={chartHistory} />

          {bestBySeason.length > 0 && (
            <section className="rank-panel history-rank-panel" aria-label="Best ranks by season">
              <div className="rank-panel-head history-rank-panel-head">
                <h3>Best ranks by season</h3>
                <p>Compare every set at a glance. The selected season is highlighted.</p>
              </div>
              <div className="history-rank-layout">
                {chartPoints.length > 0 && (
                  <div className="history-season-chart-wrap">
                    <svg
                      className="season-peak-chart"
                      viewBox={`0 0 ${chartWidth} ${chartHeight}`}
                      role="img"
                      aria-label="Peak rank by season"
                    >
                      {chartTicks.map((tick, index) => (
                        <g key={`tick-${index}`}>
                          <line className="season-peak-grid" x1={chartPadL} x2={chartWidth - chartPadR} y1={tick.y} y2={tick.y} />
                          <text className="season-peak-axis-label" x={chartWidth - chartPadR + 10} y={tick.y + 4}>
                            {tick.label}
                          </text>
                        </g>
                      ))}
                      {peaksPath && <path className="season-peak-line" d={peaksPath} />}
                      {chartPoints.map((point) => (
                        <g key={`set-${point.setNumber}`}>
                          {point.hasData ? (
                            <circle
                              className={`season-peak-dot ${point.setNumber === selectedSet ? 'selected' : ''}`}
                              cx={point.x}
                              cy={point.y ?? chartPadTop + chartUsableH}
                              r={point.setNumber === selectedSet ? 5 : 4}
                            />
                          ) : (
                            <circle
                              className={`season-peak-dot missing ${point.setNumber === selectedSet ? 'selected' : ''}`}
                              cx={point.x}
                              cy={chartPadTop + chartUsableH + 3}
                              r={3.5}
                            />
                          )}
                          <text className="season-peak-set-label" x={point.x} y={chartHeight - 10} textAnchor="middle">
                            S{point.setNumber}
                          </text>
                          <title>{`Set ${point.setNumber}: ${point.label}`}</title>
                        </g>
                      ))}
                    </svg>
                  </div>
                )}

                <ol className="history-season-list">
                  {latestSeasonPeaks.map((rank) => (
                    <li
                      key={`set-${rank.setNumber}`}
                      className={`history-season-row ${rank.setNumber === selectedSet ? 'selected' : ''}`}
                    >
                      <span className="history-season-row-set">Set {rank.setNumber}</span>
                      <div className="history-season-row-main">
                        <p className="history-season-row-rank">
                          {rank.peak
                            ? formatBestRank(rank.peak.tier, rank.peak.division, rank.peak.leaguePoints)
                            : 'No peak recorded'}
                        </p>
                        {rank.peak ? (
                          <time className="history-season-row-date" dateTime={rank.peak.recordedAt}>
                            Peak recorded {formatRecordedAt(rank.peak.recordedAt)}
                          </time>
                        ) : (
                          <span className="history-season-row-date">No ranked snapshot for this set yet</span>
                        )}
                      </div>
                    </li>
                  ))}
                </ol>
              </div>
            </section>
          )}
        </section>
      )}

      {!loading && searched && !hasData && !error && (
        <div className="empty">
          <EmptyIcon />
          <h3>No ranked timeline yet</h3>
          <p>
            Play a few Ranked TFT games, then refresh rank snapshots to build a reliable climb timeline.
          </p>
        </div>
      )}

      {!searched && !error && (
        <div className="empty">
          <EmptyIcon />
          <h3>Rank climb tracker</h3>
          <p>Search Riot ID#TAG to chart LP movement and compare your best ranks by season.</p>
        </div>
      )}
    </>
  );
}
