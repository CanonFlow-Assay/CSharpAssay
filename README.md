# CSharpAssay

CSharpAssay is a deterministic Roslyn-based verifier for explicitly selected
functional-first C# policy. It reports what it proved, what it could not prove,
and whether the toolchain itself failed. Missing evidence never becomes a clean
release verdict.

The project is currently a `0.1` research preview. All implemented rules remain
`Prototype` until their positive, negative, suppression, fault, performance, and
real-repository admission obligations are complete.

## Current capabilities

- C# 14 / .NET 10 compatibility lane;
- strict `.csassay.json` loading with a published JSON Schema;
- Roslyn diagnostics for the initial null, immutability, error, async, union,
  and suppression trust slice;
- `.csproj`, `.sln`, and `.slnx` loading through `MSBuildWorkspace`;
- explicit target-framework enumeration;
- four-state verdicts: `Pass`, `Fail`, `Inconclusive`, and `ToolFailure`;
- deterministic JSON and SARIF 2.1.0;
- conservative `set` to `init` code fix;
- report-only OneOf/ValueOf migration inventory;
- xUnit v3 on Microsoft Testing Platform.

Null safety is the first offence family. Core code must not disable nullable
analysis, use the null-forgiving operator, introduce null/default reference
values, or expose nullable public contracts. Shell code may receive and check
null only to convert it immediately into an explicit domain representation.
CSharpAssay's owned contracts use `Presence<T>` or a closed outcome instead of
nullable state. The full solution currently self-assays with zero findings.

## What CSharpAssay does not claim

CSharpAssay is not a universal “professional C#” score or an unconstrained
source rewriter. If a repository cannot compile or load, the result is
`Inconclusive` or `ToolFailure` with the failed evidence; it is never a guessed
pass.

The release goal is complete reporting within the admitted mechanical rule
contract across every loaded project and target framework. General concurrency
correctness, architecture quality, security, domain correctness, and
performance remain outside that contract unless a future rule defines and
qualifies a provable subset.

Automatic changes remain deliberately narrow. The current safe fix changes a
public record auto-property from `set` to `init`. Async and concurrency edits
must preserve cancellation, failure, ordering, and synchronization semantics,
so they remain diagnostic-only until a fix has behavioral qualification.
`migrate --report` inventories representation risk and never edits source.

## Build and test

```text
dotnet restore CSharpAssay.slnx --locked-mode
dotnet build CSharpAssay.slnx --no-restore --configuration Release
dotnet test CSharpAssay.slnx --no-build --configuration Release
```

## CLI

```text
dotnet run --project src/CsAssay.Runner -- doctor
dotnet run --project src/CsAssay.Runner -- catalog --profile compat
dotnet run --project src/CsAssay.Runner -- check CSharpAssay.slnx
dotnet run --project src/CsAssay.Runner -- verify CSharpAssay.slnx \
  --json artifacts/csassay.json \
  --sarif artifacts/csassay.sarif
```

`check` is provisional. Only `verify` is structurally capable of release
authority, and the research preview deliberately returns `Inconclusive` until
at least one rule has met the admission contract.

See [grandplan.md](grandplan.md) for the full design and
[STATUS.md](STATUS.md) for implementation progress.
