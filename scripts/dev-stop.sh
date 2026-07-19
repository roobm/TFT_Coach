#!/usr/bin/env bash
#
# Stops everything started for local TFT Coach development:
#   1. Kills the backend API (port 5214) and frontend dev server (port 5173).
#   2. Stops local Supabase (Docker).
#
# Use this when `dev-start.sh` was killed abruptly (or run in the background) and
# left processes holding their ports. Safe to run repeatedly — it ignores
# anything that is already stopped.
#
# Usage: ./scripts/dev-stop.sh [--keep-supabase]
set -euo pipefail

# Resolve the repo root regardless of where the script is called from.
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"
cd "$ROOT_DIR"

info()  { printf '\033[1;34m==>\033[0m %s\n' "$1"; }
warn()  { printf '\033[1;33m[!]\033[0m %s\n' "$1"; }

KEEP_SUPABASE=0
for arg in "$@"; do
  case "$arg" in
    --keep-supabase) KEEP_SUPABASE=1 ;;
    *) warn "Unknown argument: $arg" ;;
  esac
done

# Frontend (Vite) on 5173, backend API (Kestrel) on 5214.
DEV_PORTS=(5173 5214)

# --- kill processes holding the dev ports -------------------------------------
for port in "${DEV_PORTS[@]}"; do
  pids="$(lsof -ti "tcp:${port}" 2>/dev/null || true)"
  if [[ -n "$pids" ]]; then
    info "Stopping process(es) on port ${port}: ${pids//$'\n'/ }"
    # Try a graceful TERM first, then force-kill anything still alive.
    kill $pids >/dev/null 2>&1 || true
    sleep 1
    survivors="$(lsof -ti "tcp:${port}" 2>/dev/null || true)"
    if [[ -n "$survivors" ]]; then
      warn "Force-killing stubborn process(es) on port ${port}."
      kill -9 $survivors >/dev/null 2>&1 || true
    fi
  else
    info "Nothing listening on port ${port}."
  fi
done

# --- stop Supabase ------------------------------------------------------------
if [[ "$KEEP_SUPABASE" -eq 1 ]]; then
  info "Leaving Supabase running (--keep-supabase)."
elif command -v supabase >/dev/null 2>&1; then
  info "Stopping local Supabase..."
  supabase stop || warn "Could not stop Supabase (it may not be running)."
else
  warn "supabase CLI not found on PATH — skipping Supabase shutdown."
fi

info "All dev services stopped."
