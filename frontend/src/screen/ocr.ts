/**
 * Thin wrapper over Tesseract.js for reading small numeric HUD regions. The
 * worker is created lazily so the rest of the live-coach page isn't slowed when
 * screen reading is never used.
 */

import { createWorker, type Worker } from 'tesseract.js';
import { rectToPixels, type OcrField, type RegionRect } from './regions';
import type { OcrRead } from './parse';

// Character whitelist per field: only digits, plus the dash for stage like "3-2".
const WHITELIST: Record<OcrField, string> = {
  gold: '0123456789',
  level: '0123456789',
  health: '0123456789',
  stage: '0123456789-',
};

// Small HUD text reads far better when upscaled before OCR.
const UPSCALE = 3;

let workerPromise: Promise<Worker> | null = null;

async function getWorker(): Promise<Worker> {
  if (!workerPromise) {
    workerPromise = createWorker('eng');
  }
  return workerPromise;
}

/** Releases the OCR worker (called when screen reading is turned off). */
export async function disposeOcr(): Promise<void> {
  if (workerPromise) {
    const worker = await workerPromise;
    workerPromise = null;
    await worker.terminate();
  }
}

/**
 * Crops a region from the frame, upscales it, and returns a canvas ready for OCR.
 */
function cropRegion(
  frame: HTMLCanvasElement,
  rect: RegionRect,
): HTMLCanvasElement | null {
  const px = rectToPixels(rect, frame.width, frame.height);
  const out = document.createElement('canvas');
  out.width = px.w * UPSCALE;
  out.height = px.h * UPSCALE;
  const ctx = out.getContext('2d', { willReadFrequently: true });
  if (!ctx) {
    return null;
  }
  ctx.imageSmoothingEnabled = false;
  ctx.drawImage(frame, px.x, px.y, px.w, px.h, 0, 0, out.width, out.height);
  return out;
}

/** OCRs a single field's region out of the frame. */
export async function readField(
  frame: HTMLCanvasElement,
  field: OcrField,
  rect: RegionRect,
): Promise<OcrRead> {
  const crop = cropRegion(frame, rect);
  if (!crop) {
    return { field, text: '', confidence: 0 };
  }
  const worker = await getWorker();
  await worker.setParameters({ tessedit_char_whitelist: WHITELIST[field] });
  const { data } = await worker.recognize(crop);
  return { field, text: data.text.trim(), confidence: data.confidence };
}
