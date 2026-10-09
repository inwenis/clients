#!/usr/bin/env bash
# Run src/clients.fsproj from this checkout; logic in run.ps1.
set -euo pipefail
cd "$(dirname "$0")"
exec pwsh -NoProfile -File ./run.ps1 "$@"
