# CSharpAssay implementation status

```text
version: 0.1.0-research
stable lane: .NET SDK 10.0.301 / C# 14 / Roslyn 5.6.0
native lane: unavailable in the current environment; explicitly unqualified
release authority: disabled until rule admission
```

| Phase | State | Evidence |
| --- | --- | --- |
| 0 — platform and contract | complete on stable lane | release build: 0 warnings/errors; 30/30 tests; byte-identical JSON/SARIF reruns |
| 1 — exercises and specimens | complete on stable lane | TG01–TG15 compile/analyze/execute; rule closure, fault capture, and adjudication schema; 18/18 corpus tests |
| 2 — analyzer trust slice | in progress | 14 prototype rules; owned contracts use explicit presence; deterministic full-solution self-assay is provisional Pass at 0/0/0; 0 rules admitted |
| 3 — authoritative CLI | started | commands and four-state mapping implemented; all-solution qualification pending |
| 4 — packaging and CI | pending | project package metadata exists; workflows/package verification pending |
| 5 — migration/adapters | started | report-only OneOf/ValueOf public API inventory |
| 6 — contextual guidance | pending | no heuristic rules enabled |

## Admission position

No rule is labelled `Admitted`. The implementation therefore cannot return an
authoritative `Pass`; this is deliberate. Real-repository adjudication,
performance evidence, and the native-preview lane are still outstanding.
Suppression and fault specimens now exist for every catalogue rule.

## Current qualification boundary

The full 14-project solution builds with zero warnings/errors and all 49 tests
pass. Two independent provisional self-assays report 14 projects, zero
findings, zero missing evidence, and zero tool failures with byte-identical JSON
and SARIF.

Roslyn/MSBuildWorkspace emits 42 “project reference without a matching metadata
reference” messages while loading the complete `.slnx`. Every referenced
project is independently present in the assayed graph, so these are retained as
informational workspace diagnostics with `affectsCompleteness: false`. The same
message remains missing evidence for a lone project input when its referenced
projects are outside the assayed graph.

## Known environment fact

The host provides .NET SDK 10.0.301. No .NET 11 preview SDK is installed, so
native union detection is isolated behind the SDK adapter and recorded as
unavailable instead of inferred from source text.
