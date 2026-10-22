# Current platform qualification matrix

The active lane is the exact .NET 11 RC1 SDK in `global.json`, C# preview and
Roslyn 5.9.0. Older framework targeting has no fallback release authority.
The [previous .NET 10 matrix](historical/platform-matrix-net10.md) and admission
phase JSON files are historical records; they are not current native results.

| Capability | CSharpAssay engine | DAA application workflow |
| --- | --- | --- |
| SDK / language | Pinned .NET 11 RC1 / preview | Same exact pin |
| Project intake | Evaluated compiler/workspace graph | Explicit .csproj and .slnx |
| Native unions | Compiler capability check and native corpus | Required in authored core samples |
| Admitted rules | Seven; catalogue is authoritative | Original engine receipt retained |
| Linux/POSIX | Local build/test/evidence execution | Qualified sample orchestration; controlled local processes |
| Windows | Current full qualification not recorded here | Process groups and fcntl locks remain unqualified |
| Older targets | Explicit unsupported/missing evidence | Classified without automatic retargeting |
| Release qualification | Must include all release tests, self-assay, reproducible pack and fresh installation | ReadyForReview never waives a failed engine gate |

Use the current executed qualification report for pass/fail, source commit,
SDK, complete test counts, performance samples and package hashes. The matrix
is a scope declaration, not a replacement for those receipts. Prototype rules
remain advisory until separately admitted; .NET 11 does not promote them.
