#!/usr/bin/env bash
# Library consumed straight from GitHub via paket; nothing is installed on this machine.
if [ "$(git rev-parse --git-dir)" != "$(git rev-parse --git-common-dir)" ]; then
  echo "deploy: linked worktree, merge first and deploy from the main checkout" >&2
  exit 1
fi
echo "script empty on purpose, nothing needs to be done"
