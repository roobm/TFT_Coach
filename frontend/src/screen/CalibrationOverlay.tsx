import { useRef, useState } from 'react';
import {
  OCR_FIELDS,
  type OcrField,
  type OcrRegions,
  type RegionRect,
} from './regions';

interface CalibrationOverlayProps {
  /** A frozen frame from the shared screen, as a data URL. */
  frameDataUrl: string;
  /** Regions to start from (defaults or previously saved). */
  initialRegions: OcrRegions;
  onSave: (regions: OcrRegions) => void;
  onCancel: () => void;
}

const FIELD_LABELS: Record<OcrField, string> = {
  gold: 'Gold',
  level: 'Level',
  stage: 'Stage',
  health: 'Health',
};

interface DragState {
  startX: number;
  startY: number;
  rect: RegionRect;
}

/**
 * Lets the player draw a box over each HUD value on a frozen capture frame. The
 * boxes are stored as ratios so they hold at any resolution.
 */
export function CalibrationOverlay({
  frameDataUrl,
  initialRegions,
  onSave,
  onCancel,
}: CalibrationOverlayProps) {
  const [regions, setRegions] = useState<OcrRegions>(initialRegions);
  const [active, setActive] = useState<OcrField>('gold');
  const [drag, setDrag] = useState<DragState | null>(null);
  const surfaceRef = useRef<HTMLDivElement>(null);

  function toRatio(clientX: number, clientY: number): { x: number; y: number } {
    const el = surfaceRef.current;
    if (!el) {
      return { x: 0, y: 0 };
    }
    const bounds = el.getBoundingClientRect();
    const x = (clientX - bounds.left) / bounds.width;
    const y = (clientY - bounds.top) / bounds.height;
    return {
      x: Math.min(1, Math.max(0, x)),
      y: Math.min(1, Math.max(0, y)),
    };
  }

  function handlePointerDown(e: React.PointerEvent) {
    const { x, y } = toRatio(e.clientX, e.clientY);
    setDrag({ startX: x, startY: y, rect: { x, y, w: 0, h: 0 } });
  }

  function handlePointerMove(e: React.PointerEvent) {
    if (!drag) {
      return;
    }
    const { x, y } = toRatio(e.clientX, e.clientY);
    const rect: RegionRect = {
      x: Math.min(drag.startX, x),
      y: Math.min(drag.startY, y),
      w: Math.abs(x - drag.startX),
      h: Math.abs(y - drag.startY),
    };
    setDrag({ ...drag, rect });
  }

  function handlePointerUp() {
    if (drag && drag.rect.w > 0.005 && drag.rect.h > 0.005) {
      setRegions((prev) => ({ ...prev, [active]: drag.rect }));
    }
    setDrag(null);
  }

  return (
    <div className="ocr-cal-backdrop" role="dialog" aria-modal="true" aria-label="Calibrate screen regions">
      <div className="ocr-cal-panel">
        <div className="ocr-cal-toolbar">
          <span className="ocr-cal-hint">
            Pick a field, then drag a box over that value on your screen.
          </span>
          <div className="ocr-cal-fields">
            {OCR_FIELDS.map((field) => (
              <button
                key={field}
                type="button"
                className={`ocr-cal-chip ${active === field ? 'active' : ''}`}
                onClick={() => setActive(field)}
              >
                {FIELD_LABELS[field]}
              </button>
            ))}
          </div>
        </div>

        <div
          ref={surfaceRef}
          className="ocr-cal-surface"
          onPointerDown={handlePointerDown}
          onPointerMove={handlePointerMove}
          onPointerUp={handlePointerUp}
          onPointerLeave={handlePointerUp}
        >
          <img src={frameDataUrl} alt="Captured screen" draggable={false} />
          {OCR_FIELDS.map((field) => {
            const rect = field === active && drag ? drag.rect : regions[field];
            return (
              <div
                key={field}
                className={`ocr-cal-box ${field === active ? 'active' : ''}`}
                style={{
                  left: `${rect.x * 100}%`,
                  top: `${rect.y * 100}%`,
                  width: `${rect.w * 100}%`,
                  height: `${rect.h * 100}%`,
                }}
              >
                <span className="ocr-cal-box-label">{FIELD_LABELS[field]}</span>
              </div>
            );
          })}
        </div>

        <div className="ocr-cal-actions">
          <button type="button" className="ocr-cal-cancel" onClick={onCancel}>
            Cancel
          </button>
          <button type="button" className="ocr-cal-save" onClick={() => onSave(regions)}>
            Save calibration
          </button>
        </div>
      </div>
    </div>
  );
}
