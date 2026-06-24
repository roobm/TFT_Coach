import { useCallback, useEffect, useRef, useState } from 'react';
import { startCapture, type ScreenCapture } from './capture';
import { disposeOcr } from './ocr';
import { readScreenOnce } from './reader';
import { loadRegions, saveRegions, type OcrRegions } from './regions';
import type { OcrGameStatePartial } from './parse';

const POLL_MS = 1500;

export type ReaderStatus = 'idle' | 'starting' | 'reading' | 'error';

export interface ScreenReader {
  status: ReaderStatus;
  error: string | null;
  lastUpdated: number | null;
  /** A frozen frame for the calibration overlay, or null when not calibrating. */
  calibrationFrame: string | null;
  regions: OcrRegions;
  start: () => Promise<void>;
  stop: () => void;
  openCalibration: () => Promise<void>;
  saveCalibration: (regions: OcrRegions) => void;
  cancelCalibration: () => void;
}

/**
 * Manages the screen-reading lifecycle (capture → poll → OCR) and feeds each
 * parsed snapshot to {@link onSnapshot}. The capture stream comes from a window
 * the player explicitly shares; reading is advisory and never auto-submits.
 */
export function useScreenReader(
  onSnapshot: (partial: OcrGameStatePartial) => void,
): ScreenReader {
  const [status, setStatus] = useState<ReaderStatus>('idle');
  const [error, setError] = useState<string | null>(null);
  const [lastUpdated, setLastUpdated] = useState<number | null>(null);
  const [calibrationFrame, setCalibrationFrame] = useState<string | null>(null);
  const [regions, setRegions] = useState<OcrRegions>(() => loadRegions());

  const captureRef = useRef<ScreenCapture | null>(null);
  const regionsRef = useRef<OcrRegions>(regions);
  const busyRef = useRef(false);
  const pollRef = useRef<number | null>(null);
  const pausedRef = useRef(false);
  const onSnapshotRef = useRef(onSnapshot);

  onSnapshotRef.current = onSnapshot;
  regionsRef.current = regions;

  const teardown = useCallback(() => {
    if (pollRef.current !== null) {
      window.clearInterval(pollRef.current);
      pollRef.current = null;
    }
    captureRef.current?.stop();
    captureRef.current = null;
    void disposeOcr();
  }, []);

  const stop = useCallback(() => {
    teardown();
    setStatus('idle');
    setCalibrationFrame(null);
  }, [teardown]);

  // One OCR pass, guarded so slow OCR runs never overlap.
  const tick = useCallback(async () => {
    const capture = captureRef.current;
    if (!capture || busyRef.current || pausedRef.current) {
      return;
    }
    busyRef.current = true;
    try {
      const partial = await readScreenOnce(capture, regionsRef.current);
      if (Object.keys(partial).length > 0) {
        onSnapshotRef.current(partial);
        setLastUpdated(Date.now());
      }
    } catch {
      // Transient OCR errors are ignored; the next tick retries.
    } finally {
      busyRef.current = false;
    }
  }, []);

  // Ensures a capture stream exists, prompting the share picker if needed.
  const ensureCapture = useCallback(async (): Promise<ScreenCapture> => {
    if (captureRef.current) {
      return captureRef.current;
    }
    const capture = await startCapture();
    capture.onEnded(() => {
      teardown();
      setStatus('idle');
      setCalibrationFrame(null);
      setError('Screen sharing ended.');
    });
    captureRef.current = capture;
    return capture;
  }, [teardown]);

  const start = useCallback(async () => {
    setError(null);
    setStatus('starting');
    try {
      await ensureCapture();
      setStatus('reading');
      // Give the video a moment to produce its first frame before polling.
      pollRef.current = window.setInterval(() => void tick(), POLL_MS);
    } catch (err) {
      setStatus('error');
      setError(err instanceof Error ? err.message : 'Could not start screen capture.');
      teardown();
    }
  }, [ensureCapture, tick, teardown]);

  const openCalibration = useCallback(async () => {
    setError(null);
    try {
      const capture = await ensureCapture();
      // Allow a frame to render before grabbing it.
      await new Promise((resolve) => window.setTimeout(resolve, 300));
      const frame = capture.grabFrame();
      if (!frame) {
        setError('Could not grab a frame to calibrate. Try again.');
        return;
      }
      setCalibrationFrame(frame.toDataURL('image/png'));
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Could not start screen capture.');
    }
  }, [ensureCapture]);

  const saveCalibration = useCallback((next: OcrRegions) => {
    saveRegions(next);
    setRegions(next);
    setCalibrationFrame(null);
  }, []);

  const cancelCalibration = useCallback(() => {
    setCalibrationFrame(null);
  }, []);

  // Pause OCR while the tab is hidden to save work.
  useEffect(() => {
    function onVisibility() {
      pausedRef.current = document.hidden;
    }
    document.addEventListener('visibilitychange', onVisibility);
    return () => document.removeEventListener('visibilitychange', onVisibility);
  }, []);

  // Clean up on unmount.
  useEffect(() => teardown, [teardown]);

  return {
    status,
    error,
    lastUpdated,
    calibrationFrame,
    regions,
    start,
    stop,
    openCalibration,
    saveCalibration,
    cancelCalibration,
  };
}
