#!/usr/bin/env sh
# Point this clone at the repository-managed hooks under .githooks/
set -eu

ROOT="$(git rev-parse --show-toplevel)"
cd "$ROOT"

git config core.hooksPath .githooks

# Ensure the pre-push hook is executable on Unix / Git Bash.
if [ -f .githooks/pre-push ]; then
  chmod +x .githooks/pre-push 2>/dev/null || true
fi

echo "Git hooks installed."
echo "  core.hooksPath = $(git config --get core.hooksPath)"
echo "Pre-push will run: dotnet test Phisio.sln"
