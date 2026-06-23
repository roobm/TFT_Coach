import type { TftMatchResult } from '../api';

function placementTone(placement: number): 'first' | 'top' | 'bottom' {
  if (placement === 1) return 'first';
  if (placement <= 4) return 'top';
  return 'bottom';
}

const ORDINALS = ['', 'st', 'nd', 'rd'];

function ordinalSuffix(n: number): string {
  const v = n % 100;
  if (v >= 11 && v <= 13) return 'th';
  return ORDINALS[n % 10] ?? 'th';
}

function resultLabel(placement: number): string {
  if (placement === 1) return 'Victory';
  if (placement <= 4) return 'Top 4';
  return 'Bottom 4';
}

function formatDate(iso: string): string {
  const date = new Date(iso);
  if (Number.isNaN(date.getTime())) return '—';
  const diffMs = Date.now() - date.getTime();
  const diffMin = Math.round(diffMs / 60000);
  if (diffMin < 1) return 'Just now';
  if (diffMin < 60) return `${diffMin}m ago`;
  const diffHr = Math.round(diffMin / 60);
  if (diffHr < 24) return `${diffHr}h ago`;
  const diffDay = Math.round(diffHr / 24);
  if (diffDay < 7) return `${diffDay}d ago`;
  return date.toLocaleDateString(undefined, { month: 'short', day: 'numeric' });
}

function formatDuration(seconds: number): string {
  if (!seconds || seconds <= 0) return '—';
  const total = Math.round(seconds);
  const minutes = Math.floor(total / 60);
  const secs = total % 60;
  return `${minutes}:${secs.toString().padStart(2, '0')}`;
}

// "Version 17.1.b14..." or "Set17_14.1" -> "Patch 14.1" when a number is present.
function formatPatch(version: string | null): string | null {
  if (!version) return null;
  const match = version.match(/(\d+\.\d+)/);
  return match ? `Patch ${match[1]}` : null;
}

// Riot returns internal ids like "TFT17_DarkStar 3" or "TFT17_Briar".
// Strip the set prefix and trailing "Trait"/"UniqueTrait" noise for readability.
function cleanName(raw: string): string {
  const withoutPrefix = raw.replace(/^TFT\d+_/, '');
  const match = withoutPrefix.match(/^(.*?)(?:UniqueTrait|Trait)?(\s+\d+)?$/);
  const name = match?.[1] ?? withoutPrefix;
  const tier = match?.[2] ?? '';
  // Split camelCase into spaced words: "DarkStar" -> "Dark Star".
  const spaced = name.replace(/([a-z])([A-Z])/g, '$1 $2');
  return `${spaced}${tier}`.trim();
}

const MAX_CHIPS = 8;

function ChipGroup({
  label,
  items,
  variant,
}: {
  label: string;
  items: string[];
  variant: 'trait' | 'unit' | 'augment';
}) {
  if (items.length === 0) return null;
  const visible = items.slice(0, MAX_CHIPS);
  const hidden = items.length - visible.length;
  return (
    <div className="chip-group">
      <span className="chip-group-label">{label}</span>
      <div className="chips">
        {visible.map((item) => (
          <span key={item} className={`chip ${variant}`}>
            {cleanName(item)}
          </span>
        ))}
        {hidden > 0 && <span className="chip-more">+{hidden} more</span>}
      </div>
    </div>
  );
}

function MatchCard({ match }: { match: TftMatchResult }) {
  const tone = placementTone(match.placement);
  const patch = formatPatch(match.gameVersion);

  return (
    <li className={`match-card ${tone}`}>
      <div className="match-top">
        <span
          className={`placement ${tone}`}
          aria-label={`Placed ${match.placement}${ordinalSuffix(match.placement)}`}
        >
          <span className="rank-num">{match.placement}</span>
          <span className="rank-ord">{ordinalSuffix(match.placement)}</span>
        </span>
        <div className="match-headline">
          <div className={`match-result ${tone}`}>{resultLabel(match.placement)}</div>
          <div className="match-date">{formatDate(match.playedAt)}</div>
        </div>
        {patch && <span className="patch-badge">{patch}</span>}
      </div>

      <div className="stat-row">
        <div className="stat">
          <span className="stat-value">{match.level}</span>
          <span className="stat-label">Level</span>
        </div>
        <div className="stat">
          <span className="stat-value">{formatDuration(match.durationSeconds)}</span>
          <span className="stat-label">Duration</span>
        </div>
        <div className="stat">
          <span className="stat-value">{match.playersEliminated}</span>
          <span className="stat-label">Elims</span>
        </div>
      </div>

      <ChipGroup label="Traits" items={match.traits} variant="trait" />
      {match.augments.length > 0 && (
        <ChipGroup label="Augments" items={match.augments} variant="augment" />
      )}
      <ChipGroup label="Comp" items={match.units} variant="unit" />
    </li>
  );
}

export function MatchList({ matches }: { matches: TftMatchResult[] }) {
  // Always newest first, top to bottom.
  const ordered = [...matches].sort(
    (a, b) => new Date(b.playedAt).getTime() - new Date(a.playedAt).getTime(),
  );
  return (
    <ul className="match-list">
      {ordered.map((match) => (
        <MatchCard key={match.matchId} match={match} />
      ))}
    </ul>
  );
}
