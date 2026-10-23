# CSharpAssay

A deterministic Roslyn verifier for functional C# policy. **.NET 11 is the only supported target and runtime.** The development package line is `0.2.0-rc.1`.

`global.json` pins SDK `11.0.100-rc.1.26425.128` with roll-forward disabled. Projects use `net11.0` and `LangVersion=preview` for native C# unions. The sole profile is `native`, selected by default. Earlier frameworks, `auto`, `compat`, and third-party union/value-object library adapters are unsupported.

See the [migration and validation report](docs/net11-migration.md) for breaking changes and validation results.

The current [C# depth undertaking](docs/csharp-depth.md) expands prototype
catch-all detection while retaining the existing admission and evidence limits.

## Build and test

Install the exact pinned SDK, then run:

```sh
./eng/restore.sh
dotnet build CSharpAssay.slnx --no-restore --configuration Release
dotnet test CSharpAssay.slnx --no-build --no-restore --configuration Release --max-parallel-test-modules 1
```

Tests are serialized because the suite includes an analyzer latency gate. Older MSBuild targets produce `CSASSAY-NET11-REQUIRED`. Unsupported analyzed targets produce incomplete evidence and cannot receive a passing verification verdict.

## Commands

```sh
dotnet run --project src/CsAssay.Runner -- doctor
dotnet run --project src/CsAssay.Runner -- catalog
dotnet run --project src/CsAssay.Runner -- check CSharpAssay.slnx
dotnet run --project src/CsAssay.Runner -- verify CSharpAssay.slnx --json artifacts/csassay.json --sarif artifacts/csassay.sarif
dotnet run --project src/CsAssay.Runner -- assess CSharpAssay.slnx --json artifacts/assessment.json
```

`check` is provisional; `verify` evaluates configured release evidence and required tests. Verdicts remain `Pass`, `Fail`, `Inconclusive`, and `ToolFailure`. Missing evidence never becomes a passing verdict. Tests use xUnit v3 and Microsoft Testing Platform.

`assess` performs the same authoritative evaluation as `verify` and writes an
additive `csassay-assessment/0.1` JSON envelope. Its `assessment` projection retains
scope, source identities, verdict, authority and missing/failure evidence; its
`receipt` embeds the complete original verification JSON. Existing `verify` JSON
is unchanged. Application coordinators can consume this envelope without
promoting provisional findings or changing rule admission.

Native union exhaustiveness belongs to the C# compiler. `CSAU0004` reports discard/default arms that hide future cases; its status remains `Prototype`. Selecting .NET 11 does not promote an unqualified rule. Seven previously admitted nullability, immutability, and suppression rules remain the release gate. Contextual advice remains non-blocking.

The removed `migrate --report` command and `CSAU0003` were specific to legacy OneOf/ValueOf representations. Ordinary project-owned domain types remain analyzable.

## Packages and playground

`CsAssay.Analyzers` and `CsAssay.Tool` target .NET 11 and use version `0.2.0-rc.1`. Analyzer hosts must run .NET 11; older IDE/compiler hosts are unsupported. Local builds do not publish packages.

```sh
cd ../CSharpAssay.Playground
./eng/prepare-assay.sh ../CSharpAssay
./eng/run-assay.sh ../CSharpAssay/src/CsAssay.Runner/bin/Release/net11.0/cs-assay.dll
./eng/run-gof-crosswalk.sh
./eng/run-functional-shape.sh
```

Historical phase-admission documents and stored `0.1.x` evidence describe the earlier toolchain, not fresh .NET 11 validation.

For a complete local qualification record, run `./eng/qualify-local`
from a clean source commit. It retains every gate result under ignored
`artifacts/local-qualification/`, including later diagnostic checks after a
test failure; release qualification remains false unless every gate passes.
See [functional enforcement](docs/functional-enforcement.md) and the
[current platform matrix](eng/qualification/platform-matrix.md).
