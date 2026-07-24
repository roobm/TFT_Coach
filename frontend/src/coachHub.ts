import { HubConnection, HubConnectionBuilder, LogLevel } from '@microsoft/signalr';
import type { ApiEnvelope } from './api';

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

/** The active meta the coach is grounded on, surfaced for display. */
export interface MetaInfo {
  set: string;
  patch: string;
  comps: string[];
}

/** One meta composition with the detail rendered on the Comps page. */
export interface MetaComp {
  name: string;
  tier: string;
  playstyle: string;
  carries: string[];
  keyItems: string[];
  notes?: string;
}

/** The full current meta (set, patch, and every comp) for the Comps page. */
export interface MetaDetail {
  set: string;
  patch: string;
  comps: MetaComp[];
}

function unwrapEnvelope<T>(payload: ApiEnvelope<T>): T {
  if (!payload.success) {
    throw new Error(payload.error?.message ?? 'Request failed.');
  }
  return payload.data;
}

let connection: HubConnection | null = null;
let startPromise: Promise<void> | null = null;

// A single shared connection is lazily created and reused across suggestions.
// Concurrent callers (e.g. React StrictMode double-invokes, or fetching meta and
// a suggestion at once) all await the same in-flight start so none of them tries
// to invoke before the connection reaches the 'Connected' state.
async function getConnection(): Promise<HubConnection> {
  if (!connection) {
    connection = new HubConnectionBuilder()
      .withUrl(`${API_BASE}/hubs/coach`)
      .withAutomaticReconnect()
      .configureLogging(LogLevel.Warning)
      .build();
    connection.onclose(() => {
      startPromise = null;
    });
  }

  if (connection.state === 'Connected') {
    return connection;
  }

  if (!startPromise) {
    startPromise = connection.start();
  }
  await startPromise;
  return connection;
}

/** Opens the live coach connection (used when the coach is toggled Online). */
export async function connect(): Promise<void> {
  await getConnection();
}

/** Closes the live coach connection (used when the coach is toggled Offline). */
export async function disconnect(): Promise<void> {
  if (connection) {
    const conn = connection;
    connection = null;
    startPromise = null;
    await conn.stop();
  }
}

/** Fetches the active meta the coach is grounded on, or null if none is set. */
export async function getMeta(): Promise<MetaInfo | null> {
  const conn = await getConnection();
  const payload = await conn.invoke<ApiEnvelope<MetaInfo | null>>('GetMeta');
  return unwrapEnvelope(payload);
}

/** Fetches the full current meta (every comp with detail), or null if none is set. */
export async function getMetaComps(): Promise<MetaDetail | null> {
  const conn = await getConnection();
  const payload = await conn.invoke<ApiEnvelope<MetaDetail | null>>('GetMetaComps');
  return unwrapEnvelope(payload);
}

/** Requests a single live coaching suggestion for the given board state. */
export async function requestSuggestion(state: GameState): Promise<CoachingSuggestion> {
  const conn = await getConnection();
  const payload = await conn.invoke<ApiEnvelope<CoachingSuggestion>>('GetSuggestion', { state });
  return unwrapEnvelope(payload);
}
