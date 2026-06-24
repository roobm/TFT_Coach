/**
 * Pure parsing + validation of raw OCR text into plausible GameState fields.
 * Kept free of browser APIs so it can be unit-tested in isolation.
 */

import type { OcrField } from './regions';

/** The numeric/textual fields the OCR can fill on the live-coach form. */
export interface OcrGameStatePartial {
  gold?: number;
  level?: number;
  stage?: string;
  health?: number;
}

/** One OCR read for a region, as returned by the OCR engine. */
export interface OcrRead {
  field: OcrField;
  text: string;
  /** Engine confidence, 0..100. */
  confidence: number;
}

/** Reads below this confidence are ignored to avoid filling garbage. */
export const MIN_CONFIDENCE = 60;

/** Pulls the first run of digits out of noisy OCR text, or null if none. */
function firstInt(text: string): number | null {
  const match = text.replace(/[^0-9]/g, ' ').trim().match(/\d+/);
  if (!match) {
    return null;
  }
  const value = Number.parseInt(match[0], 10);
  return Number.isNaN(value) ? null : value;
}

/** Parses a stage like "3-2" / "3 - 2" / "3—2"; returns "N-N" or null. */
export function parseStage(text: string): string | null {
  const match = text.match(/([1-9])\s*[-—–]\s*([1-9])/);
  if (!match) {
    return null;
  }
  const stage = Number.parseInt(match[1], 10);
  const round = Number.parseInt(match[2], 10);
  if (stage < 1 || stage > 9 || round < 1 || round > 9) {
    return null;
  }
  return `${stage}-${round}`;
}

/**
 * Validates a single OCR read into a field value. Returns the typed value when it
 * is confident and plausible, otherwise undefined (so the field is left as-is).
 */
export function parseRead(read: OcrRead): OcrGameStatePartial {
  if (read.confidence < MIN_CONFIDENCE) {
    return {};
  }

  switch (read.field) {
    case 'gold': {
      const value = firstInt(read.text);
      // Gold can spike with interest; cap generously to reject misreads.
      if (value !== null && value >= 0 && value <= 200) {
        return { gold: value };
      }
      return {};
    }
    case 'level': {
      const value = firstInt(read.text);
      if (value !== null && value >= 1 && value <= 11) {
        return { level: value };
      }
      return {};
    }
    case 'health': {
      const value = firstInt(read.text);
      if (value !== null && value >= 0 && value <= 100) {
        return { health: value };
      }
      return {};
    }
    case 'stage': {
      const stage = parseStage(read.text);
      return stage ? { stage } : {};
    }
    default:
      return {};
  }
}

/** Merges a batch of reads into a single partial, last valid value winning. */
export function parseReads(reads: OcrRead[]): OcrGameStatePartial {
  let result: OcrGameStatePartial = {};
  for (const read of reads) {
    result = { ...result, ...parseRead(read) };
  }
  return result;
}
