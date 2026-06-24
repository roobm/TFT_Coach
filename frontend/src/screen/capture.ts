/**
 * Screen capture via getDisplayMedia. The player explicitly picks the TFT window
 * to share; we draw the latest frame to an offscreen canvas on demand so the OCR
 * layer can crop regions out of it. Nothing is recorded or sent anywhere.
 */

export interface ScreenCapture {
  /** Draws the current frame to an internal canvas and returns it. */
  grabFrame(): HTMLCanvasElement | null;
  /** Stops the stream and releases the display. */
  stop(): void;
  /** Registers a callback fired when the user stops sharing from the browser UI. */
  onEnded(callback: () => void): void;
}

/** Maps a getDisplayMedia failure to an actionable, human message. */
function describeCaptureError(err: unknown): string {
  const name = err instanceof DOMException ? err.name : '';
  switch (name) {
    case 'NotAllowedError':
      // Either the OS blocked it (macOS Screen Recording) or the user cancelled.
      return (
        'Screen capture was blocked. On macOS grant Screen Recording to your browser ' +
        'in System Settings ▸ Privacy & Security ▸ Screen Recording, then fully quit and ' +
        'reopen the browser. (If you just cancelled the picker, click "Read my screen" again.)'
      );
    case 'NotSupportedError':
    case 'NotFoundError':
      return 'No screen source was available to capture. Try a different window or display.';
    case 'NotReadableError':
      return 'The screen could not be read (another app may be using it). Try again.';
    default:
      return err instanceof Error && err.message
        ? err.message
        : 'Could not start screen capture.';
  }
}

/**
 * Prompts the user to share a screen/window and returns a capture handle.
 * Rejects if the API is unavailable or the user cancels the picker.
 */
export async function startCapture(): Promise<ScreenCapture> {
  if (!navigator.mediaDevices?.getDisplayMedia) {
    throw new Error(
      'Screen capture is not available here. Open the app in Chrome, Edge or Safari ' +
        '(not an embedded preview) at http://localhost:5173.',
    );
  }

  let stream: MediaStream;
  try {
    stream = await navigator.mediaDevices.getDisplayMedia({
      video: { frameRate: 2 },
      audio: false,
    });
  } catch (err) {
    throw new Error(describeCaptureError(err));
  }

  const video = document.createElement('video');
  video.srcObject = stream;
  video.muted = true;
  await video.play();

  const canvas = document.createElement('canvas');
  const track = stream.getVideoTracks()[0];

  let stopped = false;
  const endedCallbacks: Array<() => void> = [];

  function cleanup() {
    if (stopped) {
      return;
    }
    stopped = true;
    for (const t of stream.getTracks()) {
      t.stop();
    }
    video.srcObject = null;
  }

  track.addEventListener('ended', () => {
    cleanup();
    for (const cb of endedCallbacks) {
      cb();
    }
  });

  return {
    grabFrame(): HTMLCanvasElement | null {
      if (stopped || video.videoWidth === 0 || video.videoHeight === 0) {
        return null;
      }
      canvas.width = video.videoWidth;
      canvas.height = video.videoHeight;
      const ctx = canvas.getContext('2d', { willReadFrequently: true });
      if (!ctx) {
        return null;
      }
      ctx.drawImage(video, 0, 0, canvas.width, canvas.height);
      return canvas;
    },
    stop() {
      cleanup();
    },
    onEnded(callback: () => void) {
      endedCallbacks.push(callback);
    },
  };
}
