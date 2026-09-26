#!/usr/bin/env bash
set -euo pipefail
repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$repo_root"
dotnet restore CSharpAssay.slnx --locked-mode
# These compiler/workspace specimens are intentionally not buildable solutions.
for fixture in specimens/Projects/*/*.csproj; do
  dotnet restore "$fixture" --locked-mode
done
