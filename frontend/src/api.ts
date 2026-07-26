export interface UnitDetail {
  name: string;
  tier: number;
  items: string[];
}

export interface TftMatchResult {
  matchId: string;
  setNumber: number;
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

export interface ApiError {
  code: string;
  message: string;
  details?: Record<string, string[]>;
}

export interface ApiEnvelope<T> {
  success: boolean;
  data: T;
  error: ApiError | null;
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

export interface SeasonBestRank {
  setNumber: number;
  tier: string;
  division: string | null;
  leaguePoints: number;
  recordedAt: string;
}

export interface RanksResponse {
  puuid: string;
  entries: RankedEntry[];
  history: RankSnapshot[];
  bestBySeason: SeasonBestRank[];
}

const API_BASE = import.meta.env.VITE_API_BASE ?? 'http://localhost:5214';

function statusMessage(status: number): string {
  return `Request failed with status ${status}.`;
}

async function readEnvelope<T>(response: Response): Promise<T> {
  let payload: ApiEnvelope<T> | null = null;
  try {
    payload = (await response.json()) as ApiEnvelope<T>;
  } catch {
    if (!response.ok) {
      throw new Error(statusMessage(response.status));
    }
    throw new Error('Invalid API response payload.');
  }

  if (!payload.success) {
    throw new Error(payload.error?.message ?? statusMessage(response.status));
  }

  if (!response.ok) {
    throw new Error(payload.error?.message ?? statusMessage(response.status));
  }

  return payload.data;
}

export async function fetchMatchHistory(riotId: string, count = 10, set = 0): Promise<MatchHistoryResponse> {
  const params = new URLSearchParams({ riotId, count: String(count) });
  if (set !== 0) {
    params.set('set', String(set));
  }
  const response = await fetch(`${API_BASE}/matches?${params.toString()}`);
  return readEnvelope<MatchHistoryResponse>(response);
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
  return readEnvelope<AnalysisResponse>(response);
}

export async function fetchRanks(riotId: string): Promise<RanksResponse> {
  const url = `${API_BASE}/ranks?riotId=${encodeURIComponent(riotId)}`;
  const response = await fetch(url);
  return readEnvelope<RanksResponse>(response);
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
  return readEnvelope<TeamCodeResponse>(response);
}
