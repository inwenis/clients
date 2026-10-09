#!/usr/bin/env bash
# Restore the dotnet tools (paket and fantomas); build.sh restores the packages.
set -euo pipefail
cd "$(dirname "$0")"
dotnet tool restore
