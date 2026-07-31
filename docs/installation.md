# Installation, verification, and rollback

CSharpAssay 0.1 is a research preview. Pin exact package versions in source
control and introduce `check` before making `verify` a required release gate.
For a published release, NuGet.org is the default source. For pre-publication
qualification or rollback, obtain both `.nupkg` files from one trusted workflow
run and place them in a local feed such as `./packages`.

## Requirements

- .NET SDK 10.0.301 or the supported patch selected by `global.json`;
- a restored, compilable `.csproj`, `.sln`, or `.slnx` input;
- `.csassay.json` for authoritative verification;
- xUnit v3 on Microsoft Testing Platform for the currently qualified required
  test-evidence reporter.

## Analyzer package

Add the analyzer privately so it does not flow into consumers of your library:

```text
dotnet add package CsAssay.Analyzers --version 0.1.0
```

The equivalent project entry is:

```xml
<PackageReference Include="CsAssay.Analyzers"
                  Version="0.1.0"
                  PrivateAssets="all" />
```

Build once and confirm that a known qualification violation is reported before
relying on the analyzer in CI. The package contains the analyzer plus only its
three CSharpAssay-owned runtime dependencies. Roslyn is supplied by the host.

The package includes `buildTransitive` props and targets. Enforcement is on by
default: admitted blocking diagnostics fail `dotnet build`, and the target
rejects attempts to turn analyzers off or hide an admitted rule in `NoWarn`.
This does not replace `cs-assay verify`, whose authority also requires complete
workspace, policy, target-framework, and configured-test evidence.

## Command-line tool

Global installation:

```text
dotnet tool install --global CsAssay.Tool --version 0.1.0
cs-assay doctor
cs-assay catalog --profile compat
```

For a repository-pinned installation, create and commit a tool manifest:

```text
dotnet new tool-manifest
dotnet tool install CsAssay.Tool --version 0.1.0
dotnet tool restore
dotnet tool run cs-assay doctor
```

Adopt the gate in two steps:

```text
cs-assay check MySolution.slnx --json artifacts/check.json
cs-assay verify MySolution.slnx \
  --json artifacts/verify.json \
  --sarif artifacts/verify.sarif
cs-assay migrate --report MySolution.slnx \
  --json artifacts/migration.json
```

`check` is provisional and does not execute configured release tests. `verify`
is authoritative. Its exit codes are 0 Pass, 1 Fail, 2 Inconclusive, and 3
ToolFailure. Only exit 0 is a clean release decision.

`migrate --report` is not a release verdict or source rewriter. It inventories
public OneOf/ValueOf representation exposure and emits unresolved framework
qualification obligations. Read the [migration guide](migration.md) before
changing an exposed representation.

## Artifact verification

Download the two `.nupkg` files and `checksums.sha256` from the same workflow
run, then verify their bytes:

```text
sha256sum --check checksums.sha256
gh attestation verify CsAssay.Analyzers.0.1.0.nupkg \
  --repo CanonFlow-Assay/CSharpAssay
gh attestation verify CsAssay.Tool.0.1.0.nupkg \
  --repo CanonFlow-Assay/CSharpAssay
```

Main-branch CI creates keyless GitHub/Sigstore build-provenance attestations.
The NuGet packages themselves remain unsigned because the repository supplies
no NuGet signing certificate or authorized key. Do not interpret “unsigned” as
“unverified”; match the checksum and provenance to the trusted workflow run.

## Rollback

Remove the global tool:

```text
dotnet tool uninstall --global CsAssay.Tool
```

For a local manifest, use `dotnet tool uninstall CsAssay.Tool`, or update back
to the last accepted pinned version. Remove or downgrade the analyzer
`PackageReference` in the same change. If CSharpAssay was a required CI gate,
revert that workflow requirement together with the package pin so a missing
tool does not masquerade as a repository failure.

An emergency build-only rollback may set `CsAssayEnforceOnBuild=false`. Treat
that property as a reviewed incident action, not a permanent configuration.

Keep previous JSON/SARIF, checksums, provenance, and the policy file used for
the decision. Rollback changes enforcement; it must not erase the audit trail.
