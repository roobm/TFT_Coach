/**
 * Screen regions to OCR, expressed as ratios (0..1) of the captured frame so the
 * same calibration works at any resolution. The player calibrates these once by
 * drawing boxes over their HUD; the result is persisted to localStorage.
 */

export type OcrField = 'gold' | 'level' | 'stage' | 'health';

/** A rectangle as fractions of the frame width/height (all in 0..1). */
export interface RegionRect {
  x: number;
  y: number;
  w: number;
  h: number;
}

export type OcrRegions = Record<OcrField, RegionRect>;

export const OCR_FIELDS: OcrField[] = ['gold', 'level', 'stage', 'health'];

/**
 * Rough 1080p defaults for the TFT HUD. These are only a starting point — the HUD
 * shifts with resolution and UI scale, so the player is expected to calibrate.
 */
export const DEFAULT_REGIONS: OcrRegions = {
  // Gold counter sits centre-bottom above the shop.
  gold: { x: 0.475, y: 0.86, w: 0.05, h: 0.045 },
  // Level badge is bottom-left of the shop area.
  level: { x: 0.18, y: 0.86, w: 0.04, h: 0.045 },
  // Stage/round indicator is top-centre.
  stage: { x: 0.47, y: 0.02, w: 0.06, h: 0.04 },
  // Player health is shown on the left rail; very approximate by default.
  health: { x: 0.02, y: 0.45, w: 0.05, h: 0.04 },
};

const STORAGE_KEY = 'tftCoach.ocrRegions';

function isRect(value: unknown): value is RegionRect {
  if (typeof value !== 'object' || value === null) {
    return false;
  }
  const r = value as Record<string, unknown>;
  return (
    typeof r.x === 'number' &&
    typeof r.y === 'number' &&
    typeof r.w === 'number' &&
    typeof r.h === 'number'
  );
}

/** Loads saved regions, falling back to defaults for any missing/invalid field. */
export function loadRegions(): OcrRegions {
  try {
    const raw = localStorage.getItem(STORAGE_KEY);
    if (!raw) {
      return { ...DEFAULT_REGIONS };
    }
    const parsed = JSON.parse(raw) as Record<string, unknown>;
    const result = { ...DEFAULT_REGIONS };
    for (const field of OCR_FIELDS) {
      if (isRect(parsed[field])) {
        result[field] = parsed[field] as RegionRect;
      }
    }
    return result;
  } catch {
    return { ...DEFAULT_REGIONS };
  }
}

/** Persists the calibrated regions. */
export function saveRegions(regions: OcrRegions): void {
  try {
    localStorage.setItem(STORAGE_KEY, JSON.stringify(regions));
  } catch {
    // Storage may be unavailable (private mode); calibration just won't persist.
  }
}

/** Converts a ratio rect into pixel coordinates for a frame of the given size. */
export function rectToPixels(
  rect: RegionRect,
  frameWidth: number,
  frameHeight: number,
): { x: number; y: number; w: number; h: number } {
  return {
    x: Math.round(rect.x * frameWidth),
    y: Math.round(rect.y * frameHeight),
    w: Math.max(1, Math.round(rect.w * frameWidth)),
    h: Math.max(1, Math.round(rect.h * frameHeight)),
  };
}
