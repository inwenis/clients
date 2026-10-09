#!/usr/bin/env bash
# Check .fsx nuget pins against paket.lock, then dotnet build; logic in build.ps1.
set -euo pipefail
cd "$(dirname "$0")"
exec pwsh -NoProfile -File ./build.ps1 "$@"
