#!/usr/bin/env bash
# Check that the .fsx nuget pins match paket.lock.
set -euo pipefail
cd "$(dirname "$0")"
exec pwsh -NoProfile -File ./tools/version-fsx-check.ps1
