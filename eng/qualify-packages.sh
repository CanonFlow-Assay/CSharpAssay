#!/usr/bin/env bash
set -euo pipefail

phase4_script_dir=$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)
phase4_root=$(cd "$phase4_script_dir/.." && pwd)
phase4_dotnet=${DOTNET_COMMAND:-dotnet}
phase4_scratch=$(mktemp -d "${TMPDIR:-/tmp}/csassay-install.XXXXXX")
trap 'rm -rf "$phase4_scratch"' EXIT
cd "$phase4_root"

mkdir -p "$phase4_scratch/dotnet-home" "$phase4_scratch/nuget-packages"
phase4_dotnet_home="$phase4_scratch/dotnet-home"
phase4_nuget_packages="$phase4_scratch/nuget-packages"
if [[ "$phase4_dotnet" == *.exe ]] && command -v wslpath > /dev/null 2>&1; then
  phase4_dotnet_home=$(wslpath -w "$phase4_dotnet_home")
  phase4_nuget_packages=$(wslpath -w "$phase4_nuget_packages")
fi
export DOTNET_CLI_HOME="$phase4_dotnet_home"
export NUGET_PACKAGES="$phase4_nuget_packages"

"$phase4_dotnet" tool install CsAssay.Tool \
  --version 0.1.0 \
  --tool-path "$phase4_scratch/tools" \
  --configfile eng/qualification/packaging/NuGet.Config \
  --no-cache

phase4_tool="$phase4_scratch/tools/cs-assay"
if [[ ! -f "$phase4_tool" && -f "$phase4_tool.exe" ]]; then
  phase4_tool="$phase4_tool.exe"
fi
chmod +x "$phase4_tool"
"$phase4_tool" doctor
"$phase4_tool" verify \
  specimens/Projects/BoundaryScope/BoundaryScope.csproj

"$phase4_dotnet" restore \
  eng/qualification/packaging/AnalyzerConsumer/AnalyzerConsumer.csproj \
  --configfile eng/qualification/packaging/NuGet.Config \
  --no-cache
"$phase4_dotnet" build \
  eng/qualification/packaging/AnalyzerConsumer/AnalyzerConsumer.csproj \
  --no-restore \
  --configuration Release

if "$phase4_dotnet" build \
    eng/qualification/packaging/AnalyzerConsumer/AnalyzerConsumer.csproj \
    --no-restore \
    --configuration Release \
    -p:QualificationViolation=true \
    > "$phase4_scratch/analyzer-negative.log" 2>&1; then
  echo "Analyzer package failed to block its negative qualification specimen." >&2
  exit 1
fi
if ! grep -q "CSAN0001" "$phase4_scratch/analyzer-negative.log"; then
  echo "Analyzer package negative build did not report CSAN0001." >&2
  exit 1
fi

echo "Fresh tool install and analyzer package qualification passed."
