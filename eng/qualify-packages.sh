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
  --version 0.1.1 \
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
if ! grep -q "CSAN0004" "$phase4_scratch/analyzer-negative.log"; then
  echo "Analyzer package negative build did not report warning-level CSAN0004." >&2
  exit 1
fi

if "$phase4_dotnet" build \
    eng/qualification/packaging/AnalyzerConsumer/AnalyzerConsumer.csproj \
    --no-restore \
    --configuration Release \
    -p:RunAnalyzers=false \
    > "$phase4_scratch/analyzer-disabled.log" 2>&1; then
  echo "Analyzer package allowed its build gate to be disabled silently." >&2
  exit 1
fi
if ! grep -q "CSASSAY-BUILD-GATE-DISABLED" \
    "$phase4_scratch/analyzer-disabled.log"; then
  echo "Analyzer-disabled build did not report the CSharpAssay gate error." >&2
  exit 1
fi

if "$phase4_dotnet" build \
    eng/qualification/packaging/AnalyzerConsumer/AnalyzerConsumer.csproj \
    --no-restore \
    --configuration Release \
    -p:NoWarn=CSAN0001 \
    > "$phase4_scratch/analyzer-suppressed.log" 2>&1; then
  echo "Analyzer package allowed an admitted rule through NoWarn." >&2
  exit 1
fi
if ! grep -q "CSASSAY-BUILD-GATE-SUPPRESSED" \
    "$phase4_scratch/analyzer-suppressed.log"; then
  echo "NoWarn build did not report the CSharpAssay suppression error." >&2
  exit 1
fi

if "$phase4_dotnet" build \
    eng/qualification/packaging/AnalyzerConsumer/AnalyzerConsumer.csproj \
    --no-restore \
    --configuration Release \
    -p:WarningsNotAsErrors=CSAN0004 \
    > "$phase4_scratch/analyzer-demoted.log" 2>&1; then
  echo "Analyzer package allowed an admitted rule through WarningsNotAsErrors." >&2
  exit 1
fi
if ! grep -q "CSASSAY-BUILD-GATE-SUPPRESSED" \
    "$phase4_scratch/analyzer-demoted.log"; then
  echo "WarningsNotAsErrors build did not report the CSharpAssay suppression error." >&2
  exit 1
fi

if ! "$phase4_dotnet" build \
    eng/qualification/packaging/AnalyzerConsumer/AnalyzerConsumer.csproj \
    --no-restore \
    --configuration Release \
    -p:QualificationViolation=true \
    -p:TreatWarningsAsErrors=true \
    -p:CsAssayEnforceOnBuild=false \
    > "$phase4_scratch/analyzer-rollback-wae.log" 2>&1; then
  cat "$phase4_scratch/analyzer-rollback-wae.log" >&2
  echo "Reviewed rollback did not neutralize CSharpAssay warning promotion." >&2
  exit 1
fi
if ! grep -q "warning CSAN0004" \
    "$phase4_scratch/analyzer-rollback-wae.log"; then
  echo "Reviewed rollback erased the admitted CSharpAssay diagnostic." >&2
  exit 1
fi

if "$phase4_dotnet" build \
    eng/qualification/packaging/AnalyzerConsumer/AnalyzerConsumer.csproj \
    --no-restore \
    --configuration Release \
    -p:QualificationUnrelatedWarning=true \
    -p:TreatWarningsAsErrors=true \
    -p:CsAssayEnforceOnBuild=false \
    > "$phase4_scratch/analyzer-rollback-unrelated.log" 2>&1; then
  echo "Reviewed rollback demoted an unrelated compiler warning." >&2
  exit 1
fi
if ! grep -q "CS1030" \
    "$phase4_scratch/analyzer-rollback-unrelated.log"; then
  echo "Unrelated warnings-as-errors qualification did not report CS1030." >&2
  exit 1
fi

"$phase4_dotnet" build \
  eng/qualification/packaging/AnalyzerConsumer/AnalyzerConsumer.csproj \
  --no-restore \
  --configuration Release \
  -p:CsAssayEnforceOnBuild=false \
  -p:RunAnalyzers=false

echo "Fresh tool install and analyzer package qualification passed."
