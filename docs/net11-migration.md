# .NET 11 RC1 migration report

## Toolchain

- SDK: `11.0.100-rc.1.26425.128`, exact pin; no roll-forward.
- Runtime/TFM: .NET 11 / `net11.0`; platform-specific .NET 11 TFMs are supported.
- Language: C# preview, enabling native union declarations.
- Roslyn NuGet packages: `5.9.0`; MSBuild packages: `18.10.1`.
- Tool/analyzer package line: `0.2.0-rc.1` (breaking prerelease).

Microsoft's [download page](https://dotnet.microsoft.com/en-us/download/dotnet/11.0) lists the pinned SDK. The [union reference](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/builtin-types/union) describes native type unions and the .NET 11 `UnionAttribute` / `IUnion` runtime contracts. Distinct case types express discriminated alternatives.

## Implementation

Both repositories default to .NET 11 and reject older build targets. The .NET 10 release lane and optional .NET 11 probe are consolidated. Domain, catalogue, SDK adapter, analyzer, and code-fix assemblies no longer target .NET Standard. Framework collections replace the extra immutable-collections package; old polyfills are removed.

The sole policy/CLI profile is `native`. Compiler parsing and runtime union contracts are checked before native evidence is available. Older TFMs, language versions, absent union support, or a policy prohibiting the RC toolchain produce missing evidence. There is no compatibility fallback.

Removed: OneOf ordinal-extraction rule `CSAU0003`, OneOf/ValueOf migration inventory and CLI command, library adapter registries, their fixtures, and the compatibility corpus. Native corpus examples execute real code instead of returning unavailable placeholders. `CSAU0004` remains a prototype with compiler/analyzer regression coverage.

The playground consumes matching local NuGet packages. Its preparation script regenerates locks against the selected source revision using a checkout-local cache, so stale prerelease package bytes cannot survive a rebuild. GoF shipment and recursive menu use native unions. Existing project-owned domain types, behavior tests, architecture boundaries, and Assay policy gates remain. The external eShop .NET 10 case study is historical and excluded from the new CI gate.

## Compatibility and review notes

This breaks removed profile, CLI, and library-adapter APIs and support for older runtimes/IDE analyzer hosts. Roslyn advisory `RS1041` is specifically suppressed in compiler-extension projects because a .NET 11-only host is intentional. Assay diagnostic enforcement remains enabled.

The Roslyn `IsUnion` property is still marked experimental (`RSEXPERIMENTAL006`); its use is isolated and explicitly enabled in the pinned SDK adapter.

Style preferences are explicit for the newer SDK; correctness and Assay gates remain enabled. Historical admission records and old generated evidence are not relabeled as RC1 results. The paired playground CI revision must contain this CSharpAssay migration. Nothing is published to NuGet or pushed remotely by this local change.

## Validation

Completed locally on 2026-09-26 with the pinned Linux x64 SDK.

| Check | Result |
| --- | --- |
| CSharpAssay Release build | Passed, zero warnings and errors |
| CSharpAssay tests | 135 passed across seven test modules; zero skipped |
| Playground tests | 42 passed; zero skipped |
| Gilded Rose characterization | 8 of 8 passed |
| Playground, GoF, and Functional Shape evidence gates | All passed |
| GoF and Shape report repeatability | JSON and SARIF byte-identical across repeated runs |
| Release packages | Both packages passed normalized reproducibility comparison |
| Fresh package installation | Tool installation, verification, and native union consumer build passed |
| Unsupported framework/language checks | Seven checks passed, including both repository build guards |
| Dependency locks | All 36 retained lockfiles target .NET 11 only, including its Windows variant |

Core test counts: Domain 20, Analyzers 38, Workspaces 29, Runner 18, Integration 10, TypeGym 18, and Performance 2. The initial full run passed 134 of 135 tests: one self-assay assertion incorrectly required workspace warnings left over from the old target framework. It now checks that projects actually loaded. The entire integration module then passed all 10 tests; the other six modules had already passed.

Playground counts: main suite 9, GoF equivalence 10, Shape behavior 16, and Shape architecture 7. Refined playground and GoF reports are authoritative with zero findings. Shape verification is authoritative with its same three expected advisories (`CSAN0003` × 1, `CSAN0004` × 2), six loaded projects, and 23 passing tests. The seven admitted rules remain unchanged; native-union coverage does not promote prototype `CSAU0004` to admitted status.

Shape finding fingerprints changed only because the target framework is part of their identity: recomputing the new findings with the old `net10.0` identity reproduced the previous fingerprints exactly. Counts and policy strength were preserved.

Packaging was checked in a temporary clean Git snapshot of the working source, without committing either original checkout. The final reusable qualification script also compiles a real native union and rejects `net8.0`, `net10.0`, `netstandard2.0`, and C# 14 using the installed package's own guards. Framework rejection remains active when ordinary build enforcement is rolled back. Separate checks confirmed both repositories reject .NET 10.

Validation logs are retained locally under ignored `artifacts/net11-validation/` directories in both repositories. They include the initial full test run and the successful integration rerun. The SDK used for these checks is installed at `/tmp/canonflow-dotnet11`; use the pinned SDK on other machines. JSON/XML/workflow parsing, shell syntax, and whitespace checks also passed.

Before the playground CI workflow can pass remotely, its `assay_ref` input or `CSHARP_ASSAY_REF` repository variable must resolve to a CSharpAssay revision containing this migration. The packages remain local; publishing and pushing have not been performed.
