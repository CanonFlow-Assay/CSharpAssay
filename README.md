# CSharpAssay

CSharpAssay is a deterministic Roslyn-based verifier for explicitly selected
functional-first C# policy. It reports what it proved, what it could not prove,
and whether the toolchain itself failed. Missing evidence never becomes a clean
release verdict.

The project is currently a `0.1` research preview. Phase 3 provides the
authoritative CLI and Phase 2 admits seven
stable-lane rules after positive, negative, suppression, fault, semantic
matrix, performance, and real-repository evidence. The remaining seven rules
stay `Prototype` and cannot block a release verdict.

## Current capabilities

- C# 14 / .NET 10 compatibility lane;
- strict `.csassay.json` loading with a published JSON Schema;
- Roslyn diagnostics for the initial null, immutability, error, async, union,
  and suppression trust slice;
- `.csproj`, `.sln`, and `.slnx` loading through `MSBuildWorkspace`;
- explicit project-reference graph evidence and evaluated all-target-framework
  enumeration, including imported MSBuild properties;
- allowlisted required-rule and required-test release policy;
- compiler, analyzer, source, generated-code, suppression, workspace, and
  stable test-count evidence;
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
dotnet test CSharpAssay.slnx --no-build --configuration Release \
  --max-parallel-test-modules 1
```

Test modules are serialized because the solution includes a wall-clock analyzer
latency gate. Running that benchmark beside six CPU-heavy test processes
measures scheduler contention rather than analyzer latency.

## CLI

```text
dotnet run --project src/CsAssay.Runner -- doctor
dotnet run --project src/CsAssay.Runner -- catalog --profile compat
dotnet run --project src/CsAssay.Runner -- check CSharpAssay.slnx
dotnet run --project src/CsAssay.Runner -- verify CSharpAssay.slnx \
  --json artifacts/csassay.json \
  --sarif artifacts/csassay.sarif
```

`check` is provisional. `verify` is the release-authority path and can issue an
authoritative verdict for the seven admitted stable-lane rules when project,
compiler, analyzer, policy, target-framework, and configured test evidence is
complete. `check` records configured tests as `NotRun`; `verify` executes them.
The current test-evidence runner is qualified for xUnit v3 on Microsoft Testing
Platform and parses only stable TRX counts. Timestamps, host names, durations,
and temporary result paths never enter deterministic artifacts. Other test
stacks currently produce `ToolFailure` unless they expose the qualified
reporter contract; they are not silently treated as passing. Native preview
rules remain unqualified.

The release section of `.csassay.json` can require target frameworks, admitted
rules, and fixed test inputs:

```json
{
  "release": {
    "requiredTargetFrameworks": ["net10.0"],
    "requiredRules": ["CSAN0001", "CSAN0002"],
    "tests": [
      {
        "input": "tests/Acme.Tests/Acme.Tests.csproj",
        "configuration": "Release",
        "noBuild": false,
        "minimumExpectedTests": 1
      }
    ]
  }
}
```

See [grandplan.md](grandplan.md) for the full design and
[STATUS.md](STATUS.md) for implementation progress.
