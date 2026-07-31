# CSharpAssay implementation status

```text
version: 0.1.0-research
stable lane: .NET SDK 10.0.301 / C# 14 / Roslyn 5.6.0
native lane: unavailable in the current environment; explicitly unqualified
release authority: qualified on the stable lane for seven admitted rules
```

| Phase | State | Evidence |
| --- | --- | --- |
| 0 — platform and contract | complete on stable lane | release build: 0 warnings/errors; 30/30 tests; byte-identical JSON/SARIF reruns |
| 1 — exercises and specimens | complete on stable lane | TG01–TG15 compile/analyze/execute; rule closure, fault capture, and adjudication schema; 18/18 corpus tests |
| 2 — analyzer trust slice | complete on stable lane | 7 admitted / 7 prototype rules; authoritative self-assay Pass at 15 projects, 0 findings, 0 missing, 0 failures; 68/68 tests |
| 3 — authoritative CLI | started | commands and four-state mapping implemented; all-solution qualification pending |
| 4 — packaging and CI | started | stable-lane workflow matches the qualified Phase 2 commands; first hosted run and package verification pending |
| 5 — migration/adapters | started | report-only OneOf/ValueOf public API inventory |
| 6 — contextual guidance | pending | no heuristic rules enabled |

## Admission position

Seven rules are `Admitted`: unauthorized suppression, the four null-safety
rules, mutable setters, and mutable collection exposure. Their Phase 2
manifest binds the specimen closure, semantic matrix, IDE/build agreement,
performance gate, project-boundary fixture, and immutable real-repository
adjudication. The other seven rules remain Prototype and cannot block.

The native-preview lane is still outstanding and has no admitted rules.
Suppression and fault specimens exist for every catalogue rule.

## Current qualification boundary

The full 15-project solution builds with zero warnings/errors and all 68 tests
pass. Two independent authoritative self-assays report 15 projects, zero
findings, zero missing evidence, and zero tool failures with byte-identical
JSON and SARIF.

Roslyn/MSBuildWorkspace emits 46 “project reference without a matching metadata
reference” messages while loading the complete `.slnx`. Every referenced
project is independently present in the assayed graph, so these are retained as
informational workspace diagnostics with `affectsCompleteness: false`. The same
message remains missing evidence for a lone project input when its referenced
projects are outside the assayed graph.

## Known environment fact

The host provides .NET SDK 10.0.301. No .NET 11 preview SDK is installed, so
native union detection is isolated behind the SDK adapter and recorded as
unavailable instead of inferred from source text.
