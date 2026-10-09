#!/usr/bin/env bash
# Run src/clients.fsproj from this checkout.
set -euo pipefail
cd "$(dirname "$0")"
exec dotnet run --project ./src/clients.fsproj "$@"
