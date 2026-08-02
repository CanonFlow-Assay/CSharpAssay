#!/usr/bin/env bash
set -euo pipefail

phase4_script_dir=$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)
phase4_root=$(cd "$phase4_script_dir/.." && pwd)
phase4_output=${1:-"$phase4_root/artifacts/packages"}
phase4_dotnet=${DOTNET_COMMAND:-dotnet}
phase4_commit=${GITHUB_SHA:-$(git -C "$phase4_root" rev-parse HEAD)}
phase4_sdk=$("$phase4_dotnet" --version)
phase4_sdk=${phase4_sdk//$'\r'/}
phase4_sdk=${phase4_sdk//$'\n'/}
phase4_scratch=$(mktemp -d "${TMPDIR:-/tmp}/csassay-pack.XXXXXX")
trap 'rm -rf "$phase4_scratch"' EXIT
cd "$phase4_root"

if [[ -n $(git status --porcelain) ]]; then
  echo "Release packaging requires a clean working tree." >&2
  exit 1
fi

# Re-establish the repository's stable language/configuration output before
# packing. This prevents a prior local preview-parameter build from leaking
# stale binaries into a stable package.
"$phase4_dotnet" build \
  CSharpAssay.slnx \
  --no-restore \
  --no-incremental \
  --configuration Release

mkdir -p "$phase4_scratch/first" "$phase4_scratch/second"
for phase4_pack_dir in "$phase4_scratch/first" "$phase4_scratch/second"; do
  "$phase4_dotnet" pack \
    src/CsAssay.Analyzers/CsAssay.Analyzers.csproj \
    --no-build \
    --no-restore \
    --configuration Release \
    --output "$phase4_pack_dir" \
    -p:RepositoryCommit="$phase4_commit"
  "$phase4_dotnet" pack \
    src/CsAssay.Runner/CsAssay.Runner.csproj \
    --no-build \
    --no-restore \
    --configuration Release \
    --output "$phase4_pack_dir" \
    -p:RepositoryCommit="$phase4_commit"
done

for phase4_pack_name in first second; do
  mkdir -p "$phase4_scratch/$phase4_pack_name-provenance"
  "$phase4_dotnet" \
    eng/CsAssay.PackageAudit/bin/Release/net10.0/CsAssay.PackageAudit.dll \
    "$phase4_scratch/$phase4_pack_name-provenance" \
    "$phase4_commit" \
    "$phase4_sdk" \
    unsigned \
    normalize \
    "$phase4_scratch/$phase4_pack_name"/*.nupkg
done

cmp \
  "$phase4_scratch/first/CsAssay.Analyzers.0.1.2.nupkg" \
  "$phase4_scratch/second/CsAssay.Analyzers.0.1.2.nupkg"
cmp \
  "$phase4_scratch/first/CsAssay.Tool.0.1.2.nupkg" \
  "$phase4_scratch/second/CsAssay.Tool.0.1.2.nupkg"
cmp \
  "$phase4_scratch/first-provenance/checksums.sha256" \
  "$phase4_scratch/second-provenance/checksums.sha256"
cmp \
  "$phase4_scratch/first-provenance/provenance.json" \
  "$phase4_scratch/second-provenance/provenance.json"

mkdir -p "$phase4_output"
cp "$phase4_scratch/first"/*.nupkg "$phase4_output/"
cp "$phase4_scratch/first-provenance/checksums.sha256" "$phase4_output/"
cp "$phase4_scratch/first-provenance/provenance.json" "$phase4_output/"

echo "Reproducible packages written to $phase4_output"
