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

export interface AnalysisResponse {
  insights: string;
  matchesAnalyzed: number;
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

export async function analyzeMatches(matches: TftMatchResult[]): Promise<AnalysisResponse> {
  const response = await fetch(`${API_BASE}/matches/analysis`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ matches }),
  });
  if (!response.ok) {
    throw new Error(await readError(response));
  }
  return (await response.json()) as AnalysisResponse;
}
