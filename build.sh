#!/usr/bin/env bash
# Build the solution; dotnet build restores the packages.
set -euo pipefail
cd "$(dirname "$0")"
exec dotnet build "$@"
