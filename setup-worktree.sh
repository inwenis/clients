#!/usr/bin/env bash
# Restore the dotnet tools (paket, fantomas) and the paket packages for this worktree.
set -euo pipefail
cd "$(dirname "$0")"
dotnet tool restore
dotnet paket restore "$@"
