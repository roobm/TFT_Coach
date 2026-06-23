#!/usr/bin/env bash
#
# Starts everything needed for local TFT Coach development:
#   1. Verifies Docker is running and starts local Supabase.
#   2. Checks the LM Studio local server is reachable (manual app — not auto-started).
#   3. Runs the backend API and the frontend dev server together.
#
# Stop everything with Ctrl-C; this script shuts the child processes down cleanly.
# Supabase keeps running in Docker — stop it with `supabase stop`.
#
# Usage: ./scripts/dev-start.sh
set -euo pipefail

# Resolve the repo root regardless of where the script is called from.
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"
cd "$ROOT_DIR"

info()  { printf '\033[1;34m==>\033[0m %s\n' "$1"; }
warn()  { printf '\033[1;33m[!]\033[0m %s\n' "$1"; }
error() { printf '\033[1;31m[x]\033[0m %s\n' "$1" >&2; }

# --- .env ---------------------------------------------------------------------
if [[ ! -f .env ]]; then
  warn ".env not found — copying from .env.example. Fill in real values (e.g. RIOT_API_KEY)."
  cp .env.example .env
fi
# Export .env so the helper checks below can read LM_STUDIO_URL etc.
set -a
# shellcheck disable=SC1091
source .env
set +a

# --- prerequisites ------------------------------------------------------------
for cmd in dotnet npm supabase; do
  if ! command -v "$cmd" >/dev/null 2>&1; then
    error "Required command '$cmd' is not installed or not on PATH."
    exit 1
  fi
done

# --- Docker + Supabase --------------------------------------------------------
if ! docker info >/dev/null 2>&1; then
  error "Docker does not appear to be running. Start Docker Desktop and re-run."
  exit 1
fi

info "Starting local Supabase (idempotent)..."
supabase start

# --- LM Studio check ----------------------------------------------------------
# Use the host URL, not the docker-internal one, for the reachability check.
LM_CHECK_URL="${LM_STUDIO_URL:-http://localhost:1234/v1}"
LM_CHECK_URL="${LM_CHECK_URL/host.docker.internal/localhost}"
if curl -sf "${LM_CHECK_URL%/}/models" >/dev/null 2>&1; then
  info "LM Studio server reachable at ${LM_CHECK_URL}."
else
  warn "LM Studio not reachable at ${LM_CHECK_URL}. Open LM Studio, load your models, and start its local server (port 1234)."
fi

# --- backend + frontend -------------------------------------------------------
pids=()
cleanup() {
  info "Shutting down dev processes..."
  # ${pids[@]:-} guards against macOS bash 3.2 erroring on an empty array under `set -u`.
  for pid in "${pids[@]:-}"; do
    [[ -n "$pid" ]] && kill "$pid" >/dev/null 2>&1 || true
  done
  wait >/dev/null 2>&1 || true
  info "Done. Supabase is still running — stop it with 'supabase stop'."
}
trap cleanup INT TERM EXIT

info "Starting backend API (http://localhost:5214)..."
dotnet run --project src/TFTCoach.API &
pids+=($!)

if [[ ! -d frontend/node_modules ]]; then
  info "Installing frontend dependencies..."
  (cd frontend && npm install)
fi

info "Starting frontend dev server (http://localhost:5173)..."
(cd frontend && npm run dev) &
pids+=($!)

info "All set. Press Ctrl-C to stop the API and frontend."
wait
