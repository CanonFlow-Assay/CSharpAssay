#!/usr/bin/env bash
set -euo pipefail

preview_script_dir=$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)
preview_root=$(cd "$preview_script_dir/.." && pwd)
preview_dotnet=${DOTNET_PREVIEW_COMMAND:-"$preview_root/artifacts/dotnet-preview/dotnet"}

if [[ "$preview_dotnet" != */* ]]; then
  preview_dotnet=$(command -v "$preview_dotnet" || true)
fi

if [[ ! -x "$preview_dotnet" ]]; then
  echo "Preview SDK host not found: $preview_dotnet" >&2
  echo "Install it with dotnet-install --channel 11.0 --quality preview." >&2
  exit 66
fi

cd "$preview_script_dir/preview"
preview_version=$("$preview_dotnet" --version)
echo "CSharpAssay native probe using .NET SDK $preview_version"
export CsAssayPreviewBuild=true

"$preview_dotnet" restore \
  "$preview_root/CSharpAssay.slnx" \
  --locked-mode

"$preview_dotnet" build \
  "$preview_root/CSharpAssay.slnx" \
  --no-restore \
  --configuration Release \
  -p:LangVersion=preview \
  -p:EnforceCodeStyleInBuild=false

mkdir -p "$preview_root/artifacts/preview"
set +e
"$preview_dotnet" \
  "$preview_root/src/CsAssay.Runner/bin/Release/net10.0/cs-assay.dll" \
  check "$preview_root/CSharpAssay.slnx" \
  --profile native \
  --json "$preview_root/artifacts/preview/csassay.json" \
  --sarif "$preview_root/artifacts/preview/csassay.sarif"
preview_exit=$?
set -e

if [[ $preview_exit -ne 0 && $preview_exit -ne 2 ]]; then
  echo "Native preview probe failed with unexpected exit $preview_exit." >&2
  exit "$preview_exit"
fi

echo "Native preview probe completed with non-authoritative exit $preview_exit."
