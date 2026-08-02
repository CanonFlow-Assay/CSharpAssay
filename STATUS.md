# CSharpAssay implementation status

```text
version: 0.1.2-research
stable lane: .NET SDK 10.0.301 / C# 14 / Roslyn 5.6.0
native lane: .NET 11 SDK Preview 6 probe installed; explicitly unqualified
release authority: qualified on the stable lane for seven admitted rules
```

| Phase | State | Evidence |
| --- | --- | --- |
| 0 — platform and contract | complete on stable lane | release build: 0 warnings/errors; 30/30 tests; byte-identical JSON/SARIF reruns |
| 1 — exercises and specimens | complete on stable lane | TG01–TG15 compile/analyze/execute; rule closure, fault capture, and adjudication schema; 18/18 corpus tests |
| 2 — analyzer trust slice | complete on stable lane | 7 admitted / 7 prototype rules; authoritative self-assay Pass at 15 projects, 0 findings, 0 missing, 0 failures; 68/68 tests |
| 3 — authoritative CLI | complete on stable lane | strict command/policy contract; graph/all-TFM orchestration; compiler/analyzer/generated/suppression/test evidence; 84/84 tests; clean repeatable authority Pass |
| 4 — packaging and CI | complete on stable lane | analyzer/tool packages; commit-bound reproducible provenance; isolated fresh-install qualification; stable/preview workflow separation; SARIF and keyless attestation |
| 5 — migration/adapters | complete on stable lane | deterministic report-only OneOf/ValueOf inventory; exact evidence; explicit unqualified adapter obligations; 91/91 tests |
| 6 — contextual guidance | complete on stable lane | 6 prototype advisory families; exact glossary flow; full semantic specimen closure; staged adoption templates; transitive fail-the-build guard; passwordless manual NuGet workflow |

## Admission position

Seven rules are `Admitted`: unauthorized suppression, the four null-safety
rules, mutable setters, and mutable collection exposure. Their Phase 2
manifest binds the specimen closure, semantic matrix, IDE/build agreement,
performance gate, project-boundary fixture, and immutable real-repository
adjudication. The other thirteen rules remain Prototype and cannot block.

The native-preview lane is still outstanding and has no admitted rules.
Suppression and fault specimens exist for every catalogue rule.

## Current qualification boundary

The full 16-project solution builds with zero warnings/errors and all 107 tests
pass. Two independent authoritative self-assays report 16 project
compilations, 18 required tests passed, 10 explicitly advisory findings, zero
missing evidence, and zero tool failures with byte-identical JSON and SARIF.

Phase 3 records policy/profile negotiation, analyzer identity and hash,
project-reference edges, all evaluated target frameworks, compiler diagnostics,
source hashes, generated code, suppressions, required-rule outcomes, and stable
test counts. `check` is explicitly provisional and does not run configured
release tests. `verify` is authoritative and does.

Phase 4 packages `CsAssay.Analyzers` and `CsAssay.Tool` at version 0.1.2. Two
independent packs are canonicalized and compared byte for byte; their embedded
repository commit, identity, version, license, readme, required payload, signing
state, and exclusion of source/inspiration content are audited before checksums
and provenance are emitted. Qualification uses an isolated package cache and a
local-only feed to install the tool, verify a sample, build a clean analyzer
consumer with global warning promotion disabled, and prove that warning-level
`CSAN0004` blocks its negative specimen solely through the packaged target.
The packaged transitive target also proves that disabling analyzers or demoting
an admitted ID through `NoWarn`/`WarningsNotAsErrors` fails explicitly, while a
named emergency rollback property remains available.

Version 0.1.1 is a real-consumer hotfix prompted by Visual Studio and eShop
trials. It makes the transitive warning gate effective, excludes generated
framework sources from owned-source diagnostics, recognizes `object.Equals`
and `ReferenceEquals` null observations, and regression-tests public help URIs.

Phase 5 adds a deterministic, source-preserving `migrate --report` path. Its
qualification fixture produces 14 exact public OneOf/ValueOf exposures and
zero failures; independent JSON runs are byte-identical. Recommendations bind
the exact API, source span, metadata, representation and declaring assembly
identity/version, target
framework, compat/native behavior comparison, representation risks, and the
applicable System.Text.Json, EF Core, ASP.NET Core/OpenAPI, and NativeAOT
obligations. Those framework integrations remain unqualified rather than
being inferred from syntax. Vogen, dunet, and Thinktecture remain disabled
pending exact-version executable evidence.

Phase 6 adds `CSAD0001`/`CSAD0002`, `CSAF0001`/`CSAF0002`, `CSAE0002`, and
`CSAI0003`. They cover configured primitive obsession, possible state flags,
restricted strategy/visitor/builder shapes, simple accumulation loops,
selected explicit exceptions on owned public methods, and mutable ordinary
class contract leakage. All are Prototype, contextual or heuristic, and
Advise-only. Requiring one in consumer policy produces Inconclusive
`CSASSAY-REQUIRED-RULE-NOT-ADMITTED`; it cannot become an accidental blocker.
The repository self-assay retains 10 advice findings to prove that advisory
evidence and an authoritative pass can coexist honestly.

Roslyn/MSBuildWorkspace emits 46 “project reference without a matching metadata
reference” messages while loading the complete `.slnx`. Every referenced
project is independently present in the assayed graph, so these are retained as
informational workspace diagnostics with `affectsCompleteness: false`. The same
message remains missing evidence for a lone project input when its referenced
projects are outside the assayed graph.

## Known environment fact

The stable host uses .NET SDK 10.0.301. An isolated .NET 11 SDK
`11.0.100-preview.6.26359.118` is locally qualified through `eng/run-preview.sh`.
It builds all 16 projects cleanly, then returns a provisional Inconclusive with
16 explicit native-capability gaps and zero tool failures. The SDK compiler is
Roslyn 5.9 and exposes `ITypeSymbol.IsUnion` plus union syntax; the latest public
Roslyn NuGet dependency qualified for the shipped tool is 5.6. Native authority
therefore remains behind the SDK adapter until a reproducible 5.9 dependency or
separate preview adapter is qualified. The preview workflow cannot block the
stable lane.

The repository supplies no NuGet signing certificate or authorized private
key. Packages therefore remain explicitly unsigned. Main-branch CI produces
GitHub/Sigstore keyless build-provenance attestations over the tested package
bytes. A manual `publish-nuget.yml` workflow now has publication authority only
after the NuGet owner configures the exact trusted-publisher policy and GitHub
`nuget.org` environment. It stores no long-lived NuGet API key.

Required test execution is currently qualified for xUnit v3 on Microsoft
Testing Platform with its TRX reporter. A test stack that cannot emit that
evidence returns `ToolFailure`; it is never converted into a pass.
