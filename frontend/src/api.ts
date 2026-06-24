export interface UnitDetail {
  name: string;
  tier: number;
  items: string[];
}

export interface TftMatchResult {
  matchId: string;
  playedAt: string;
  durationSeconds: number;
  queueId: number;
  gameVersion: string | null;
  placement: number;
  level: number;
  playersEliminated: number;
  goldLeft: number;
  lastRound: number;
  totalDamageToPlayers: number;
  traits: string[];
  augments: string[];
  units: UnitDetail[];
}

export interface MatchHistoryResponse {
  puuid: string;
  matches: TftMatchResult[];
  currentSet: number;
  set: number;
}

export type PlacementTrend = 'Improving' | 'Declining' | 'Stable';

export interface CompPerformance {
  comp: string;
  games: number;
  averagePlacement: number;
}

export interface AnalysisResponse {
  matchesAnalyzed: number;
  averagePlacement: number;
  top4Rate: number;
  winRate: number;
  averageLevel: number;
  trend: PlacementTrend;
  topComps: CompPerformance[];
  strengths: string[];
  weaknesses: string[];
  suggestions: string[];
  narrative: string;
}

export interface RankedEntry {
  queueType: string;
  tier: string | null;
  division: string | null;
  leaguePoints: number;
  wins: number;
  losses: number;
  hotStreak: boolean;
  totalGames: number;
  winRate: number;
}

export interface RankSnapshot {
  queueType: string;
  tier: string | null;
  division: string | null;
  leaguePoints: number;
  wins: number;
  losses: number;
  recordedAt: string;
}

export interface RanksResponse {
  puuid: string;
  entries: RankedEntry[];
  history: RankSnapshot[];
}

const API_BASE = import.meta.env.VITE_API_BASE ?? 'http://localhost:5214';

async function readError(response: Response): Promise<string> {
  const text = await response.text();
  return text || `Request failed with status ${response.status}.`;
}

export async function fetchMatchHistory(riotId: string, count = 10, set = 0): Promise<MatchHistoryResponse> {
  const params = new URLSearchParams({ riotId, count: String(count) });
  if (set > 0) {
    params.set('set', String(set));
  }
  const response = await fetch(`${API_BASE}/matches?${params.toString()}`);
  if (!response.ok) {
    throw new Error(await readError(response));
  }
  return (await response.json()) as MatchHistoryResponse;
}

export async function analyzeMatches(
  matches: TftMatchResult[],
  puuid?: string,
): Promise<AnalysisResponse> {
  const response = await fetch(`${API_BASE}/matches/analysis`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ matches, puuid }),
  });
  if (!response.ok) {
    throw new Error(await readError(response));
  }
  return (await response.json()) as AnalysisResponse;
}

export async function fetchRanks(riotId: string): Promise<RanksResponse> {
  const url = `${API_BASE}/ranks?riotId=${encodeURIComponent(riotId)}`;
  const response = await fetch(url);
  if (!response.ok) {
    throw new Error(await readError(response));
  }
  return (await response.json()) as RanksResponse;
}

export interface DecodedUnit {
  name: string;
  cost: number;
}

export interface TeamCodeResponse {
  set: string;
  units: DecodedUnit[];
}

export async function decodeTeamCode(code: string): Promise<TeamCodeResponse> {
  const response = await fetch(`${API_BASE}/teamcode`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ code }),
  });
  if (!response.ok) {
    throw new Error(await readError(response));
  }
  return (await response.json()) as TeamCodeResponse;
}
