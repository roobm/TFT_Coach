export interface TftMatchResult {
  matchId: string;
  playedAt: string;
  durationSeconds: number;
  gameVersion: string | null;
  placement: number;
  level: number;
  playersEliminated: number;
  traits: string[];
  augments: string[];
  units: string[];
}

export interface MatchHistoryResponse {
  puuid: string;
  matches: TftMatchResult[];
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

const API_BASE = import.meta.env.VITE_API_BASE ?? 'http://localhost:5214';

async function readError(response: Response): Promise<string> {
  const text = await response.text();
  return text || `Request failed with status ${response.status}.`;
}

export async function fetchMatchHistory(riotId: string, count = 10): Promise<MatchHistoryResponse> {
  const url = `${API_BASE}/matches?riotId=${encodeURIComponent(riotId)}&count=${count}`;
  const response = await fetch(url);
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
