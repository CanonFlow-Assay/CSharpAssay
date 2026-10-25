# Adopt CSharpAssay without surprise enforcement

The current development line is **0.2.0-rc.1**, .NET 11 only. Use exact SDK
`11.0.100-rc.2.26504.105` from `global.json`, roll-forward disabled, and C# preview
for native unions. Runtime support and preview language qualification are separate.
The historical 0.1.x releases, including 0.1.2, do not describe this native build.
This guide does **not** claim that the development package is published on NuGet.

## Build and install from the local feed

From a clean, committed engine checkout with the pinned SDK on PATH:

```bash
./eng/restore.sh
dotnet build CSharpAssay.slnx --no-restore -c Release
./eng/pack-release.sh
NUGET_PACKAGES="$PWD/artifacts/dev-tool-cache" dotnet tool install CsAssay.Tool --version 0.2.0-rc.1 \
  --tool-path artifacts/dev-tools \
  --configfile eng/qualification/packaging/NuGet.Config --no-cache
./artifacts/dev-tools/cs-assay explain CSAI0001
```

The packaging configuration clears public feeds and points to the local
`artifacts/packages` directory. Packing checks clean source and reproducibility;
it publishes nothing. Use fresh tool and isolated cache directories for a new build so a cached
package with the same development version cannot masquerade as the new bytes.
For complete release qualification use `./eng/qualify-local --output
artifacts/csharp-tooling-qualified`; a successful pack alone is not qualification.

## 1. Observe and understand

Copy `eng/templates/observe.csassay.json` into your application's `.csassay.json`.
Run the locally installed tool (substitute its absolute path outside this checkout):

```bash
./artifacts/dev-tools/cs-assay doctor
./artifacts/dev-tools/cs-assay doctor /absolute/path/MySolution.slnx --policy /absolute/path/.csassay.json
./artifacts/dev-tools/cs-assay check /absolute/path/MySolution.slnx \
  --policy /absolute/path/.csassay.json \
  --json artifacts/observe.json --sarif artifacts/observe.sarif
./artifacts/dev-tools/cs-assay explain CSAI0001
```

`check` is analysis only and can exit zero with findings. Read the specific code
shape, small fix example and limits before changing a contract. All catalogue
IDs have offline bundled guides; prototype/advisory status stays visible.
A record or `init` property is not proof of a deeply immutable object graph.
The contextual doctor syntax is now implemented: it reports SDK/pin, runtime,
process MSBuild/Roslyn identity, symbol capability and declared policy separately.
It restores/builds nothing, never verifies application behavior and is not a
release gate. See [precise prerequisite checks](agent-overhaul/toolchain-prerequisite.md).

## 2. Declare real boundaries

Use `eng/templates/core.csassay.json` as a starting point, replacing **every**
`Acme.*` path/namespace with real ones. Project selectors are literal paths
relative to the policy file. The packaged analyzer and verifier now share this
policy: nearest ancestor `.csassay.json`, or an explicit analyzer
`CsAssayPolicyPath`/CLI `--policy`. Build/editor hosts consume declared
AdditionalFiles and compiler-visible project/namespace identities. See
[shared boundaries](agent-overhaul/boundaries-and-output.md).

Framework transport, serializer, persistence and HTTP behavior can remain in a
declared shell and map inputs into the core. Do not move business decisions to
shell merely to pass. Shell findings stay visible. Default is a distinct scope;
it must not be relabeled shell. An analyzer sees one compilation; solution
verification separately validates selectors against its evaluated graph. A policy
parse alone cannot establish those matches. Historical 0.1.x analyzer/CLI scope
limitations are not instructions for this development build.

Seven rules remain admitted: CSAI0001/2, CSAN0001–4 and CSAP0001. Prototype
catalogue disposition is not effective blocking severity. Do not promote it or
list a prototype as a required rule. Keep nullable flow enabled; neither shell
scope nor a grant supplies missing compiler evidence.

## 3. Configure and require verification evidence

Start with `eng/templates/strict.csassay.json`. Declare actual required test
projects, configurations and stable minimum counts; the template is not your
application's test contract. With the correct paths:

```bash
./artifacts/dev-tools/cs-assay verify /absolute/path/MySolution.slnx \
  --policy /absolute/path/.csassay.json \
  --json artifacts/verify.json --sarif artifacts/verify.sarif \
  --html artifacts/verify.html --markdown artifacts/verify.md
```

Review complete scope, configured required tests, missing evidence and execution
failures. No tests configured does not mean business behavior is covered. Native
union exhaustiveness belongs to the C# compiler; expense thresholds belong to
application tests, not inferred business knowledge in CSharpAssay.
The readable reports share console/SARIF effective severity, distinguish default
from shell, and show authority, missing evidence, tests and recorded identities.
The [CI example](../eng/templates/ci-summary.yml) retains failed-run outputs and
displays Markdown without swallowing verification's failure. SARIF upload is
optional presentation, not the release gate.

The analyzer's transitive build gate defaults `CsAssayEnforceOnBuild=true` and
rejects supported disabled-analysis/NoWarn/demotion bypasses. It supplies fast
feedback; authoritative `verify` also evaluates complete configured evidence.
A temporary reviewed `CsAssayEnforceOnBuild=false` is **unqualified rollback**,
not a passing verification. It retains admitted diagnostics as warnings, rather
than waiving unrelated warnings-as-errors. Restore it and reverify after rollback.

## 4. Review exceptions and upgrades

Use only the current fingerprinted owner/reason/expiry grant model. A code action
is an explicit proposed edit; preview it and run affected callers/tests. There is
no unattended `--fix`, blanket ignore, baseline exemption or second issue store.
Fingerprint v1 includes source position and message; a line insertion can change
identity. Do not silently transfer grants. A partial scan cannot prove a grant is
unused across a solution.

Retain current JSON/SARIF and failed runs, upgrade analyzer/tool together, and
compare scope/tool/analyzer/policy identities. Future report-only comparison
would need a separately versioned tracking identity. Changed-lines-only analysis
cannot replace full configured verification: dependencies can affect unchanged
files. New-code/baseline adoption gates, if designed later, must be named
separately from authoritative Pass and are deferred here.

Continue with the [ExpenseGuard first change](expenseguard-first-change.md).
