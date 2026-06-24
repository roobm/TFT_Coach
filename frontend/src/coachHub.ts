import { HubConnection, HubConnectionBuilder, LogLevel } from '@microsoft/signalr';

const API_BASE = import.meta.env.VITE_API_BASE ?? 'http://localhost:5214';

export type SuggestionCategory = 'Economy' | 'Board' | 'Items' | 'Comp' | 'Scout';

/** A snapshot of the player's current board, sent to the live coach. */
export interface GameState {
  stage: string;
  gold: number;
  level: number;
  health: number;
  streak: number;
  units: string[];
  items: string[];
  notes?: string;
}

export interface CoachingSuggestion {
  category: SuggestionCategory;
  text: string;
}

let connection: HubConnection | null = null;

// A single shared connection is lazily created and reused across suggestions.
async function getConnection(): Promise<HubConnection> {
  if (connection && connection.state === 'Connected') {
    return connection;
  }

  if (!connection) {
    connection = new HubConnectionBuilder()
      .withUrl(`${API_BASE}/hubs/coach`)
      .withAutomaticReconnect()
      .configureLogging(LogLevel.Warning)
      .build();
  }

  if (connection.state === 'Disconnected') {
    await connection.start();
  }

  return connection;
}

/** Requests a single live coaching suggestion for the given board state. */
export async function requestSuggestion(state: GameState): Promise<CoachingSuggestion> {
  const conn = await getConnection();
  return conn.invoke<CoachingSuggestion>('GetSuggestion', { state });
}
