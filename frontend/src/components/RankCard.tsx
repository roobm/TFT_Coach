import type { RankedEntry } from '../api';

const QUEUE_LABELS: Record<string, string> = {
  RANKED_TFT: 'Ranked',
  RANKED_TFT_DOUBLE_UP: 'Double Up',
  RANKED_TFT_TURBO: 'Hyper Roll',
};

const TIER_COLORS: Record<string, string> = {
  IRON: '#7c7268',
  BRONZE: '#a9714b',
  SILVER: '#9faec0',
  GOLD: '#f5c451',
  PLATINUM: '#4fd1c5',
  EMERALD: '#3ddc84',
  DIAMOND: '#6aa6ff',
  MASTER: '#c061f7',
  GRANDMASTER: '#ff6b7a',
  CHALLENGER: '#7ad6ff',
};

function queueLabel(queueType: string): string {
  return QUEUE_LABELS[queueType] ?? queueType.replace(/^RANKED_TFT_?/, '').replace(/_/g, ' ');
}

function titleCase(value: string): string {
  return value.charAt(0) + value.slice(1).toLowerCase();
}

function rankText(entry: RankedEntry): string {
  if (!entry.tier) {
    return 'Unranked';
  }
  const tier = titleCase(entry.tier);
  // Master+ tiers have no division.
  return entry.division ? `${tier} ${entry.division}` : tier;
}

function RankEntryCard({ entry }: { entry: RankedEntry }) {
  const accent = entry.tier ? TIER_COLORS[entry.tier] ?? 'var(--accent)' : 'var(--text-faint)';
  const winRate = Math.round(entry.winRate * 100);

  return (
    <div className="rank-card" style={{ ['--rank-accent' as string]: accent }}>
      <div className="rank-card-head">
        <span className="rank-queue">{queueLabel(entry.queueType)}</span>
        {entry.hotStreak && (
          <span className="rank-streak" title="On a win streak">Win streak</span>
        )}
      </div>

      <div className="rank-tier">
        <span className="rank-emblem" aria-hidden="true" />
        <div>
          <span className="rank-tier-name">{rankText(entry)}</span>
          {entry.tier && <span className="rank-lp">{entry.leaguePoints} LP</span>}
        </div>
      </div>

      <div className="rank-record">
        <span className="rank-record-wl">
          <span className="rank-win">{entry.wins}W</span>
          <span className="rank-sep">·</span>
          <span className="rank-loss">{entry.losses}L</span>
        </span>
        <span className="rank-winrate">{winRate}% win</span>
      </div>

    </div>
  );
}

export function RankCard({
  riotId,
  entries,
  peakLabel,
}: {
  riotId: string;
  entries: RankedEntry[];
  peakLabel?: string | null;
}) {
  // Show meaningful queues first; standard ranked before the rest.
  const ordered = [...entries].sort((a, b) => {
    const rank = (q: string) => (q === 'RANKED_TFT' ? 0 : q === 'RANKED_TFT_DOUBLE_UP' ? 1 : 2);
    return rank(a.queueType) - rank(b.queueType);
  });
  return (
    <section className="rank-panel" aria-label="Ranked standing">
      <div className="rank-panel-head">
        <span className="rank-player">{riotId}</span>
        <h3>Ranked standing</h3>
      </div>

      {ordered.length === 0 ? (
        <p className="rank-unranked">Unranked this set — play ranked games to earn a rank.</p>
      ) : (
        <div className="rank-grid">
          {ordered.map((entry) => (
            <RankEntryCard key={entry.queueType} entry={entry} />
          ))}
          {peakLabel && (
            <article className="rank-card rank-card-peak">
              <div className="rank-card-head">
                <span className="rank-queue">Season peak</span>
              </div>
              <p className="rank-peak-card-value">{peakLabel}</p>
            </article>
          )}
        </div>
      )}
    </section>
  );
}
