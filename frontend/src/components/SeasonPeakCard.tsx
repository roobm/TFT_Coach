import type { RankedEntry, RankSnapshot, TftMatchResult } from '../api';

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
const MASTER_FLOOR = TIERS.indexOf('MASTER') * 400;
const LP_DELTA: Record<number, number> = { 1: 40, 2: 30, 3: 20, 4: 10, 5: -10, 6: -20, 7: -30, 8: -40 };

function titleCase(value: string): string {
  return value.charAt(0) + value.slice(1).toLowerCase();
}

function absoluteLp(tier: string | null, division: string | null, leaguePoints: number): number | null {
  if (!tier) return null;
  const index = TIERS.indexOf(tier);
  if (index < 0) return null;
  if (index >= TIERS.indexOf('MASTER')) {
    return MASTER_FLOOR + leaguePoints;
  }
  return index * 400 + (division ? DIVISION_OFFSET[division] ?? 0 : 0) + leaguePoints;
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

function estimatedPoints(matches: TftMatchResult[], current: RankedEntry | undefined): number[] {
  if (!current) return [];
  const anchor = absoluteLp(current.tier, current.division, current.leaguePoints);
  if (anchor === null) return [];

  const ranked = matches
    .filter((match) => match.queueId === RANKED_TFT_QUEUE)
    .slice()
    .sort((a, b) => new Date(a.playedAt).getTime() - new Date(b.playedAt).getTime());
  if (ranked.length === 0) return [];

  const deltas = ranked.map((match) => LP_DELTA[match.placement] ?? 0);
  const lpAfter: number[] = new Array(ranked.length);
  lpAfter[ranked.length - 1] = anchor;
  for (let i = ranked.length - 2; i >= 0; i--) {
    lpAfter[i] = lpAfter[i + 1] - deltas[i + 1];
  }

  return lpAfter.map((lp) => Math.max(0, lp));
}

function realPoints(history: RankSnapshot[]): number[] {
  return history
    .filter((snapshot) => snapshot.queueType === 'RANKED_TFT')
    .map((snapshot) => absoluteLp(snapshot.tier, snapshot.division, snapshot.leaguePoints))
    .filter((lp): lp is number => lp !== null);
}

export function getSeasonPeakLabel({
  current,
  matches,
  history,
}: {
  current: RankedEntry | undefined;
  matches: TftMatchResult[];
  history: RankSnapshot[];
}): string | null {
  const points = [...estimatedPoints(matches, current), ...realPoints(history)];
  if (points.length === 0) {
    return null;
  }

  const peak = Math.max(...points);
  return lpToRankWithPoints(Math.round(peak));
}
