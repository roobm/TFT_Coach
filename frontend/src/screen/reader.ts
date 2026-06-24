/**
 * Orchestrates one screen-reading pass: grab a frame, OCR every calibrated
 * region, and parse the reads into a validated GameState partial.
 */

import type { ScreenCapture } from './capture';
import { readField } from './ocr';
import { parseReads, type OcrGameStatePartial, type OcrRead } from './parse';
import { OCR_FIELDS, type OcrRegions } from './regions';

/** Result of one screen-reading pass: the validated partial plus the raw reads. */
export interface ReadResult {
  partial: OcrGameStatePartial;
  reads: OcrRead[];
}

/** Reads all fields once. Returns empty results if no frame is available. */
export async function readScreenOnce(
  capture: ScreenCapture,
  regions: OcrRegions,
): Promise<ReadResult> {
  const frame = capture.grabFrame();
  if (!frame) {
    return { partial: {}, reads: [] };
  }

  const reads: OcrRead[] = [];
  for (const field of OCR_FIELDS) {
    reads.push(await readField(frame, field, regions[field]));
  }
  return { partial: parseReads(reads), reads };
}
