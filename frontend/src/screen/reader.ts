/**
 * Orchestrates one screen-reading pass: grab a frame, OCR every calibrated
 * region, and parse the reads into a validated GameState partial.
 */

import type { ScreenCapture } from './capture';
import { readField } from './ocr';
import { parseReads, type OcrGameStatePartial, type OcrRead } from './parse';
import { OCR_FIELDS, type OcrRegions } from './regions';

/** Reads all fields once. Returns an empty partial if no frame is available. */
export async function readScreenOnce(
  capture: ScreenCapture,
  regions: OcrRegions,
): Promise<OcrGameStatePartial> {
  const frame = capture.grabFrame();
  if (!frame) {
    return {};
  }

  const reads: OcrRead[] = [];
  for (const field of OCR_FIELDS) {
    reads.push(await readField(frame, field, regions[field]));
  }
  return parseReads(reads);
}
