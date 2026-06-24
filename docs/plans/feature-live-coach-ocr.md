# Feature: Live Coach — read my own screen (OCR auto-fill)

## Goal
Let the player auto-fill the Live Coach `GameState` (stage, gold, level, health,
streak, and later units/items) by reading **their own** TFT window on screen,
instead of typing every field. The OCR is an **input source only**: it fills the
existing form fields; the player still triggers the suggestion. No change to the
coaching pipeline (`GetSuggestion` use case, hub, prompt).

## Why this is ToS-defensible
- Reads only pixels the player can already see, from a window they **explicitly
  choose to share** (browser `getDisplayMedia` prompt each session).
- **Advisory only** — never auto-plays; the player reviews auto-filled values and
  presses "Get suggestion" themselves. Riot policy forbids tools that "dictate
  player decisions" or reveal hidden info; this does neither.
- Local/personal use; no client injection, no memory reading, no distributed
  overlay.

## Approach decision
Two tracks were considered:

| | Track A — Browser capture (CHOSEN for v1) | Track B — Native macOS sidecar (future) |
|---|---|---|
| Capture | `navigator.mediaDevices.getDisplayMedia()` → canvas frames | ScreenCaptureKit / `screencapture` |
| OCR | Tesseract.js (digit whitelist) in the browser | Apple Vision OCR (on-device, very accurate) |
| Install | none — runs in existing React app | separate local process |
| Platform | cross-platform | macOS-locked |
| Perf | ~1–2 Hz, JS OCR (fine for numbers) | high FPS, native |
| Consent story | strong (user picks the window) | needs Screen Recording permission |

**v1 = Track A.** Lowest risk, no native install, in-page, cross-platform, and the
share-picker gives a clean consent story. Track B is a later accuracy/perf upgrade
that can feed the same fields.

## Architecture
- All OCR lives in the **frontend** for v1 (no backend changes). It writes into the
  same React state that the manual form already binds to, so `requestSuggestion`
  and the whole Clean Architecture backend are untouched.
- Encapsulate capture+OCR in a small module so a native provider (Track B) could
  later replace it behind the same "produce a partial `GameState`" contract.

## Scope for v1
**In scope (numeric HUD fields, high-confidence OCR):**
- Gold, Level, Stage (e.g. `3-2`), Health.
- Manual region calibration (drag boxes over gold/level/stage/health once; ratios
  saved to `localStorage`), because HUD position varies by resolution/UI scale.
- Auto-poll at ~1 Hz while "Read screen" is active; debounced field updates.
- Confidence gating: only overwrite a field when OCR confidence is high and the
  value is plausible (e.g. gold 0–100+, level 1–11, health 0–100).
- The player can still edit any field by hand after auto-fill.

**Out of scope for v1 (note as follow-ups):**
- Units/items recognition (needs icon/template matching, not digit OCR).
- Streak detection (no reliable on-screen number; stays manual).
- Native Track B sidecar.
- Fully automatic suggestion triggering (kept manual on purpose — policy + UX).

## Ordered task list
1. **Branch + plan** — `git checkout -b feature/live-coach-ocr`; commit this plan.
2. **Capture module** (`frontend/src/screen/capture.ts`): start/stop a
   `getDisplayMedia` stream, draw the latest frame to an offscreen canvas, expose
   `grabFrame()` and `stop()`. Handle the user cancelling the share and the stream
   ending (`track.onended`).
3. **OCR module** (`frontend/src/screen/ocr.ts`): wrap Tesseract.js with a digit
   whitelist (`0123456789-`); `readRegion(canvas, rect)` → `{ text, confidence }`.
   Lazy-load the worker so the rest of the page isn't slowed.
4. **Region calibration** (`frontend/src/screen/regions.ts` + a small calibration
   UI): let the user draw boxes for gold/level/stage/health on a frozen frame;
   store as ratios in `localStorage` (`tftCoach.ocrRegions`). Provide sensible
   1080p defaults.
5. **Parsing/validation**: map each region's OCR text to a `GameState` field with
   range checks; ignore implausible reads; parse `stage` as `N-N`.
6. **Wire into `LiveCoachPage`**: add a "Read my screen" toggle + "Calibrate"
   button to the existing form. While active, poll ~1 Hz and set the same
   `gold/level/stage/health` state hooks the inputs already use. Show a small
   "auto-filled" indicator and last-updated time. Keep the manual inputs editable.
7. **Permissions/UX edge cases**: handle share denied, stream ended (auto-disable
   toggle), tab hidden (pause polling via `visibilitychange`), and a clear "reading
   only the window you share" note.
8. **Tests**: unit-test the parsing/validation (`ocr-parse.test.ts`) with sample
   OCR strings → expected `GameState` partials and rejected garbage. Capture/OCR
   browser APIs are mocked or left as manual QA.
9. **Docs + manual QA**: verify against a live/replay TFT window on macOS; note
   calibration steps in the page UI. Update this plan as tasks complete.

## Affected layers
- **Frontend only** for v1: new `frontend/src/screen/*` modules + edits to
  `LiveCoachPage.tsx`; new dev dependency `tesseract.js`.
- **Backend: none** (deliberately — OCR feeds the existing `GameState`).

## Risks & mitigations
- **OCR fragility** (resolution/skin/patch/UI scale) → user calibration + ratio
  regions + confidence gating + always-editable fields.
- **Units/items unreadable by digit OCR** → out of scope v1; stay manual.
- **Policy gray area** → advisory-only, own-screen-only, local, no auto-play.
- **Perf** → 1 Hz poll, lazy OCR worker, pause when tab hidden.

## Definition of done (v1)
- Player shares their TFT window, calibrates once, and gold/level/stage/health
  auto-fill and stay roughly in sync while playing.
- Fields remain manually editable; suggestion still triggered by the player.
- Parsing/validation unit tests pass; `npm run build` clean.
