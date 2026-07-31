# CSharpAssay Grand Plan

```text
product:        CSharpAssay
executable:     cs-assay
assemblies:     CsAssay.*
plan version:   1.0.0
status:         phase-6-complete-on-stable-lane
date:           2026-07-31
stable floor:   C# 14 / .NET 10 LTS
preview lane:   C# 15 working set / .NET 11 Preview 6
```

> A deterministic Roslyn-based verification application that checks whether a
> C# codebase follows an explicitly selected functional-first policy, records
> what it could and could not prove, and never turns missing evidence into a
> clean verdict.

CSharpAssay is the C# sibling of FsAssay. It is not a port of F# syntax rules,
and it is not a general claim that C# should imitate F#. It applies the same
trust discipline to C# while respecting C#'s language, runtime, frameworks,
tooling, and unavoidable object-oriented boundaries.

No LLM participates in the verdict path.

---

## 0. Executive decision

Build CSharpAssay as two products sharing one rule catalogue:

1. a normal Roslyn analyzer and code-fix package for editor/build feedback; and
2. an authoritative `cs-assay verify` .NET tool that owns project loading,
   compiler evidence, analyzer failures, policy, suppressions, tests, output,
   and the four-state release verdict.

The product supports two representation profiles:

| Profile | Stable target | Algebraic outcomes | Value objects | Release posture |
| --- | --- | --- | --- | --- |
| `compat` | C# 14 / .NET 10 LTS | `OneOf`, named closed record hierarchies, and reviewed source-generator unions | native records/`readonly record struct`; recognize legacy `ValueOf` | default and production-capable |
| `native` | C# 15 / .NET 11 | native `union` declarations and custom `[Union]` types | the same native value-object patterns; generators remain optional | preview-only until .NET 11 GA and the pinned compiler passes the qualification corpus |

`auto` is a negotiation mode, not a third rule philosophy. It selects `compat`
or `native` per compilation and target framework. This matters for
multi-targeted solutions where `net10.0` and `net11.0` coexist.

The stable baseline remains .NET 10 after .NET 11 ships. .NET 10 is LTS through
November 2028, while odd-numbered .NET releases use the shorter STS track.
CSharpAssay therefore must not make native unions a prerequisite for an
authoritative pass.

### Product boundary

CSharpAssay verifies mechanically specified obligations such as:

- nullable analysis is enabled and not locally defeated without evidence;
- selected domain data is immutable at its public surface;
- a proven closed outcome is handled exhaustively;
- expected failures use an approved typed representation at configured core
  boundaries;
- suppressions, generated-code exclusions, skipped rules, analyzer crashes, and
  project-load failures are visible;
- the same source, policy, compiler, packages, and target frameworks produce
  the same normalized result.

It does **not** prove:

- business correctness;
- mathematical purity;
- security completeness;
- the absence of all exceptions or mutation;
- that LINQ is always faster or clearer than a loop;
- that a library is good merely because it has `Option`, `Result`, or `OneOf`
  in its API;
- that a clean CSharpAssay run makes a system production-ready.

---

## 1. Corrections to the originating brief

The originating notes contain valuable direction, but several claims must not
become architecture.

### 1.1 What .NET 11 actually provides

As of 2026-07-31:

- .NET 11 Preview 6 is the current preview and is not generally supported for
  production use.
- C#'s union work is publicly testable, and the runtime now carries
  `UnionAttribute` and `IUnion`.
- The SDK still reports C# 14 as its stable language support; union declarations
  require `<LangVersion>preview</LangVersion>`.
- The Roslyn feature-status document still lists unions as **in progress**.
- The current declaration form is a type union such as
  `public union Pet(Cat, Dog, Bird);`.
- The compiler performs exhaustiveness analysis for union switches. Missing
  cases produce compiler warnings, not a magical total-function runtime.
- A generated union declaration is currently a struct whose value is held as
  `object?`; value-type cases are boxed.
- System.Text.Json and ASP.NET Core union integration appeared during the
  preview train, but their final contracts remain part of the preview
  qualification surface.

No published .NET 11 contract promises built-in `System.Result<T,E>` or
`System.Option<T>`. CSharpAssay must not design against hypothetical types.

### 1.2 The proposed generic global aliases do not compile

C# using aliases may name a closed constructed generic type, but cannot declare
an open generic alias. This is invalid:

```csharp
global using AssayResult<T, E> = OneOf.OneOf<T, E>;
```

Migration cannot be “change two global usings.” It requires either:

1. a stable project-owned result abstraction;
2. named domain cases consumed through ordinary pattern matching; or
3. an accepted public API change.

The plan uses the first two options and measures the third.

### 1.3 `readonly record struct` is not a .NET 11 feature

Record structs arrived years earlier. Teams can use them on the .NET 10
compatibility lane. They are useful for small value-semantic wrappers, but are
not automatically zero-cost:

- they can be boxed through interfaces, `object`, reflection, and current
  native union storage;
- `default(T)` exists for structs and can bypass a smart constructor;
- record equality over nested collections can still have surprising semantics;
- serialization and ORM integration must be tested.

“Value type” must never be translated into “stack allocated” as a universal
claim. The JIT and containing storage decide where values live.

### 1.4 OneOf is supported, not canonized

OneOf remains a useful compatibility option:

- generic unions from two through multiple alternatives;
- `Match`/`Switch` require one delegate per alternative;
- `OneOfBase` supports named wrappers;
- `TryPickTn` offers controlled imperative extraction.

It is not a transparent future native union:

- its public API and native union pattern-matching API differ;
- ordinal names such as `T0`, `T1`, and `AsT0` can leak into application code;
- changing a public `OneOf<T,E>` return type is an API break;
- the latest NuGet release at research time is 3.0.271 from May 2024.

CSharpAssay recognizes OneOf by metadata identity and validates safe usage. It
does not require target projects to adopt it.

### 1.5 ValueOf is legacy-compatible, not the modern default

ValueOf is small and understandable, but its latest NuGet release at research
time is 2.0.31 from March 2022. Its built-in validation contract throws. A
private derived constructor does not turn the inherited public `From` factory
into a total smart constructor.

Policy:

- recognize ValueOf in existing .NET 10 code;
- include one legacy interop exercise;
- do not recommend it as the default for new CSharpAssay examples;
- prefer a small native record/class with a total factory when that is enough;
- evaluate maintained generators such as Vogen and Thinktecture during Phase 0
  for teams needing converters, EF integration, analyzers, and boilerplate
  generation;
- never let a generator-specific API become the core rule model.

### 1.6 Native unions are not necessarily a performance upgrade

The current native union representation always stores `object?` and boxes
value-type cases. OneOf and source-generated unions have different layout and
allocation tradeoffs. CSharpAssay will not produce performance advice from
syntax alone. A migration report can flag representation changes, but a
performance verdict requires benchmarks for the actual workload.

### 1.7 Ecosystem position: adapters, not package dogma

The current ecosystem solves different problems. CSharpAssay should recognize
semantics rather than declaring one package universally best.

| Choice | Best fit | Important tradeoff |
| --- | --- | --- |
| nullable `T?` | simple absence | not a typed failure reason or composable validation |
| OneOf | ad hoc one-of-many results with enforced `Match` arity | ordinal API can leak; public migration is breaking |
| ErrorOr | lightweight application result/error workflows | opinionated error model rather than arbitrary domain union |
| CSharpFunctionalExtensions | pragmatic `Result`/`Maybe` and DDD workflows | library conventions become part of application style |
| LanguageExt | broad FP system including option/either/validation/effects | large surface and a distinct C# dialect |
| dunet | generated named union cases | build-time generator and generated API dependency |
| Vogen | generated single-value objects plus analyzers/integration | validation/default-value conventions require review |
| Thinktecture Runtime Extensions | value objects, smart enums, and generated unions with integrations | broader generator/runtime ecosystem |
| ValueOf | small legacy primitive wrapper | stale release and exception-based validation |
| native C# union | compiler-aware cases and exhaustiveness | preview, current boxing layout, evolving ecosystem integration |

Adapters begin as compatibility observations. They become admitted evidence
providers only after exact versions pass the package qualification corpus.

---

## 2. Source audit and exercise-first foundation

This plan is based on the following inspected sources:

| Source | Revision/state used | What transfers |
| --- | --- | --- |
| local FsAssay | `653a47be6e1a2906a08035886ba173c377c9c031` | evidence-bounded verdicts, agent/judge separation, rule admission, specimens, CLI/IDE separation |
| `functional-csharp` | `f9909563a3607c7f15bada12a8ac83020f1038aa` | records, pattern matching, immutable data, typed errors, functions as values, LINQ pipelines, compatibility and native unions |
| `functional-csharp-code-2` | `8d37775d80a71d09aa8ad8168168bd9c257f4980` | progressive exercises and examples from HOFs through options, either, validation, traversal, async, state, observables, and event sourcing |
| official C#/.NET sources | current on 2026-07-31 | union preview reality, support lifecycle, Roslyn status, configuration, and alias constraints |

The local inspiration repository's `Exercises` directory is already populated.
It is a nested, ignored repository with 237 line-ending-only changes. It is
source material, not the place to implement CSharpAssay. Do not normalize,
commit, or silently rewrite it.

### 2.1 What the existing exercises teach

| Existing chapters | Durable lesson | Modern CSharpAssay successor |
| --- | --- | --- |
| 2-3 | delegates, higher-order functions, pure core / impure shell | functions as dependencies; effect-boundary exercise |
| 5-6 | smart constructors, `Option`, `Map`, `Bind` | nullable versus explicit option; total value-object factory |
| 7 | composition and LINQ | expression pipelines with allocation/deferred-execution tests |
| 8-10 | `Either`, `Exceptional`, `Apply`, LINQ query pattern | typed result, error accumulation versus short circuit, async composition |
| 9 | partial application and functions as capabilities | delegates instead of one-method strategy interfaces |
| 12 | immutable lists/trees, map/bind, allocation complexity | immutable collections and structural equality |
| 13-17 examples | transitions, event sourcing, validation, HTTP boundary | functional core / imperative shell reference application |
| 18-19 examples | observables, agents, state | effect ownership, cancellation, concurrency, controlled mutation |

The book corpus is a lead, not a modern oracle. It includes older nullable
assumptions, throwing partial functions, mutable examples shown for contrast,
and a custom functional library. Every borrowed idea must be re-expressed and
retested on the selected current SDK.

### 2.2 The tracked curriculum to create

Create a new tracked `CsAssay.TypeGym` instead of modifying `inspire/`.

Each challenge has:

```text
Challenge/
  README.md
  Bad.cs
  Compat.cs
  Native.cs              # only where the representation differs
  Challenge.Tests.cs
  expected.compat.json
  expected.native.json
```

Verification compiles and runs analyzers. Text searches such as
`code.Contains("record")` are forbidden as proof.

Initial challenge sequence:

| ID | Challenge | Compat solution | Native solution | Primary obligation |
| --- | --- | --- | --- | --- |
| TG01 | immutable order data | records + immutable collection | same | no public mutation hole |
| TG02 | validated email | total factory returning typed result | same or native result union | parse, do not throw |
| TG03 | customer/order IDs | small validated wrappers | same | no primitive interchange |
| TG04 | payment outcome | named cases / OneOf | native union | exhaustive consumption |
| TG05 | railway pipeline | `Map`/`Bind` over stable facade | same facade over native representation | stable API seam |
| TG06 | accumulate validation | validation applicative | native cases, same semantics | do not confuse validation with fail-fast result |
| TG07 | replace strategy class | `Func<T,R>` | same | functions as values |
| TG08 | loop-to-pipeline judgment | LINQ and justified loop variants | same | suggestion is not dogma |
| TG09 | async boundary | `Task<Result<T,E>>` with cancellation | same | no `async void`, no blocking wait |
| TG10 | resource boundary | catch selected I/O exceptions and convert | same | exceptions at the edge |
| TG11 | state transition | pure `(state,event) -> result` | native event union | illegal transitions explicit |
| TG12 | HTTP adapter | match result to response at edge | native union + ASP.NET integration | collapse effects once |
| TG13 | serialization | round-trip every case | native union JSON | wire compatibility evidence |
| TG14 | EF/value object | converter-backed value object | same | persistence does not dictate domain primitives |
| TG15 | migration | same golden behaviors under both TFMs | both | no find-and-replace fantasy |

Every analyzer rule must have at least:

- one isolated positive specimen;
- one isolated negative specimen;
- a suppression specimen;
- malformed-code and analyzer-failure coverage;
- compatibility and native specimens when applicability differs;
- a real-world adjudication row before it can block.

---

## 3. Constitution

### Law Zero — agent/judge separation

An agent whose code is under assay must not change the unreviewed judge it is
trying to pass.

Judge-controlled assets include:

- the rule catalogue;
- severities, certainty, and disposition;
- profile mappings;
- suppression policy and baseline;
- positive/negative Gold specimens;
- evidence schema;
- package/compiler lock;
- CI verdict mapping.

### Law One — honest uncertainty

```text
missing required semantic/project evidence => Inconclusive
analyzer exception                         => ToolFailure
analyzer load failure                      => ToolFailure
project or required TFM load failure       => ToolFailure
unsupported compiler/rule combination      => Inconclusive or ToolFailure
none of the above                          => never silently Pass
```

### Law Two — deterministic blocking

```text
DefaultBlock(f)
  iff f.Rule.Status = Admitted
  and f.Rule.Certainty = Deterministic
  and f.Rule.Disposition = Block
  and f.RequiredEvidence = Complete
```

Contextual and heuristic rules are suggestions or inconclusive findings unless
a reviewed project policy supplies the missing fact.

### Law Three — reproducible verdict

```text
same(
  SourceSnapshot,
  ProjectGraph,
  TFMSet,
  ParseOptions,
  CompilationOptions,
  Policy,
  RuleCatalogue,
  AnalyzerSet,
  PackageGraph,
  Toolchain)
=> same(NormalizedVerdict)
```

Paths, timestamps, process IDs, and hostnames may appear in metadata but never
in finding identity.

### Law Four — visible suppression

Every pragma, `SuppressMessage`, analyzer-config override, baseline entry,
generated-code exclusion, file exclusion, skipped rule, and failed rule appears
in JSON/SARIF evidence.

Expired, unused, widened, malformed, or unauthorized suppressions cannot produce
a release pass.

### Law Five — honest non-claim

`Pass` means the admitted, enabled, mechanically defined obligations completed
under the recorded inputs. It is not certification of idiomaticity, security,
correctness, or purity.

### Law Six — full release authority

IDE diagnostics and `cs-assay check` are provisional. Only `cs-assay verify`
over the declared project graph and required target frameworks can produce a
release verdict.

### Law Seven — the compiler owns language truth

CSharpAssay must not reimplement a compiler rule when Roslyn already provides
the required fact. Compiler nullability and native-union exhaustiveness
diagnostics are ingested as evidence. CSharpAssay adds policy only where the
compiler has no project-specific obligation.

---

## 4. Verdict and evidence model

### 4.1 Four-state verdict

```csharp
public abstract record AssayVerdict
{
    public sealed record Pass(EvidenceBundle Evidence) : AssayVerdict;
    public sealed record Fail(
        ImmutableArray<Finding> Blocking,
        EvidenceBundle Evidence) : AssayVerdict;
    public sealed record Inconclusive(
        ImmutableArray<MissingEvidence> Missing,
        EvidenceBundle Evidence) : AssayVerdict;
    public sealed record ToolFailure(
        ImmutableArray<EvaluationFailure> Failures,
        EvidenceBundle Evidence) : AssayVerdict;
}
```

Ordering and exit codes:

```text
Pass < Inconclusive < Fail < ToolFailure

0 = Pass
1 = Fail
2 = Inconclusive
3 = ToolFailure
```

Worst result wins across projects and target frameworks.

### 4.2 Rule evaluation

```csharp
public abstract record RuleEvaluation
{
    public sealed record Completed(
        ImmutableArray<Finding> Findings) : RuleEvaluation;
    public sealed record Skipped(SkipReason Reason) : RuleEvaluation;
    public sealed record Failed(RuleFailure Failure) : RuleEvaluation;
}
```

An empty finding collection is not interchangeable with `Skipped` or `Failed`.

### 4.3 Rule record

One machine-readable catalogue is the source of truth:

```csharp
public sealed record RuleRecord(
    string Id,
    string Title,
    RuleCategory Category,
    RuleStatus Status,
    RuleCertainty Certainty,
    RuleDisposition Disposition,
    ImmutableArray<AssayProfile> Profiles,
    EvidenceRequirement RequiredEvidence,
    string Mechanism,
    SuppressionPolicy Suppression,
    string PositiveSpecimen,
    string NegativeSpecimen,
    string Documentation,
    string? DelegatedTo);
```

Generate from it:

- `SupportedDiagnostics`;
- analyzer descriptors;
- default `.globalconfig` files;
- `cs-assay catalog`;
- rule documentation;
- JSON schema projections;
- SARIF `tool.driver.rules`;
- profile tables;
- admission reports.

Catalogue status is not evidence. A rule is `Admitted` only after its proof
obligations are green in the same CI run.

### 4.4 Evidence bundle

The normalized evidence bundle records:

- repository-relative source hashes;
- dirty-state declaration;
- project graph and every evaluated TFM;
- SDK, runtime, MSBuild, Roslyn assembly, and language versions;
- parse and compilation options;
- preview-feature state;
- resolved package versions relevant to recognized representations;
- analyzer identities and hashes;
- compiler diagnostics;
- each rule's `Completed`, `Skipped`, or `Failed` result;
- generated-code decisions;
- suppressions and policy overrides;
- tests invoked and their exit/results evidence;
- profile negotiation;
- deterministic JSON and SARIF artifacts.

---

## 5. Two representation profiles

### 5.1 `compat`: C# 14 / .NET 10 LTS

This is the default profile and remains fully supported after .NET 11 GA.

Recognized sum representations:

1. `OneOf<T0,...>` and named `OneOfBase` wrappers;
2. a project-owned stable `Result<T,E>`/`Option<T>` abstraction;
3. a structurally proven closed hierarchy:
   - abstract record base;
   - constructor inaccessible to external derivation;
   - all cases nested or otherwise enumerated by reviewed configuration;
   - every case sealed;
4. reviewed source-generator output such as dunet or Thinktecture, behind a
   metadata adapter;
5. explicit nullable `T?` for simple absence.

Policy must distinguish:

- absence (`T?`/`Option<T>`);
- expected fail-fast outcome (`Result<T,E>`/OneOf);
- accumulating validation;
- exceptional infrastructure failure.

They are not interchangeable merely because each has a `Match` method.

### 5.2 `native`: C# 15 / .NET 11

Until GA, this profile is named `native-preview` in evidence even if the CLI
accepts `--profile native`.

Rules:

- the compiler is the authority for native union case coverage;
- a discard arm on a union switch is suspicious because it can suppress the
  useful “new case not handled” warning;
- nullable/default union states must be handled according to compiler flow
  state;
- value-type case boxing and serialization changes are migration evidence, not
  static performance failures;
- custom `[Union]` implementations must satisfy soundness, stability, and
  creation-equivalence obligations before CSharpAssay treats their exhaustive
  switches as deterministic;
- union JSON, ASP.NET binding/results, OpenAPI, AOT, and reflection behavior are
  tested, not inferred.

Default release policy rejects unsupported preview toolchains. A project may
explicitly opt into preview assessment, but the evidence records that this is
not the stable production lane.

### 5.3 Profile negotiation

`auto` evaluates each compilation:

```text
net11.0 + preview language + IUnion support => native-preview
otherwise                                  => compat
```

The negotiation result is evidence. It must never depend on parsing source text
for the word `union`.

---

## 6. Migration architecture

### 6.1 Recommended public contract: named outcomes

For public domain APIs, prefer named cases and ordinary pattern matching over
exposing ordinal `OneOf<T0,T1>` types:

```csharp
public sealed record PaymentApproved(Receipt Receipt);
public sealed record PaymentDeclined(DeclineReason Reason);
public sealed record PaymentUnavailable(ServiceError Error);
```

Compatibility declaration:

```csharp
public abstract record PaymentOutcome
{
    private protected PaymentOutcome() { }
}

public sealed record PaymentApproved(Receipt Receipt) : PaymentOutcome;
public sealed record PaymentDeclined(DeclineReason Reason) : PaymentOutcome;
public sealed record PaymentUnavailable(ServiceError Error) : PaymentOutcome;
```

Native declaration, subject to final syntax:

```csharp
public sealed record PaymentApproved(Receipt Receipt);
public sealed record PaymentDeclined(DeclineReason Reason);
public sealed record PaymentUnavailable(ServiceError Error);

public union PaymentOutcome(
    PaymentApproved,
    PaymentDeclined,
    PaymentUnavailable);
```

Consumers use `switch` in both lanes. Compatibility switches carry the
restricted, analyzer-verified unreachable arm; native switches remove it and
allow the compiler to prove coverage.

### 6.2 Stable railway facade

Where pervasive `Map`/`Bind` is needed, define a library-owned
`AssayResult<T,E>` facade with a deliberately small API:

```csharp
public interface IAssayResult<T, E>
{
    R Match<R>(Func<T, R> success, Func<E, R> failure);
}
```

The final concrete API is decided by the Phase 0 spike. Requirements:

- same public API and semantics on `net10.0` and `net11.0`;
- no public OneOf ordinals;
- `Map`, `Bind`, `MapError`, `Match`, async traversal, and equality laws tested;
- no implicit exception-based validation;
- JSON behavior opt-in and versioned;
- no claim that the facade can later change representation without binary,
  layout, or serialization review.

Do not ship this runtime package in the analyzer MVP. First prove that existing
libraries cannot satisfy the stable seam with acceptable complexity.

### 6.3 Direct OneOf use

Direct OneOf is acceptable for internal application seams and local outcomes.
CSharpAssay recommends:

- named case record types;
- `Match`/`Switch` in business logic;
- `TryPickTn` only for measured or interop paths;
- no unguarded `AsTn`;
- no reliance on alternative position outside the declaring module;
- no public API exposure when native migration is a stated goal.

### 6.4 Value-object migration

Changing `ValueOf<T,TThis>` classes to record structs changes:

- reference/value semantics;
- default value behavior;
- generic constraints;
- boxing;
- serializer and ORM behavior;
- binary/public API shape.

It is not a mechanical migration. `cs-assay migrate --report` inventories these
risks and emits no automatic rewrite until golden compatibility tests prove a
safe local transformation.

---

## 7. Architecture

### 7.1 Solution shape

```text
CSharpAssay.slnx
global.json
Directory.Build.props
Directory.Packages.props

src/
  CsAssay.Domain/
  CsAssay.Catalogue/
  CsAssay.SdkAdapter/
  CsAssay.Analyzers/
  CsAssay.CodeFixes/
  CsAssay.Workspaces/
  CsAssay.Runner/
  CsAssay.Reporting/

tests/
  CsAssay.Domain.Tests/
  CsAssay.Analyzers.Tests/
  CsAssay.Workspaces.Tests/
  CsAssay.Runner.Tests/
  CsAssay.Integration.Tests/
  CsAssay.Performance.Tests/

specimens/
  Compat.Bad/
  Compat.Good/
  Native.Bad/
  Native.Good/
  Faults/
  RealWorld/

typegym/
  CsAssay.TypeGym/

eng/
  qualification/
  schemas/
  baselines/
```

### 7.2 `CsAssay.Domain`

Contains pure, Roslyn-free product types:

- verdicts;
- findings;
- source locations;
- rule evaluations;
- evidence records;
- policy/profile types;
- deterministic fingerprinting.

This project must not reference analyzer, MSBuild, console, JSON, or file-system
packages.

### 7.3 `CsAssay.SdkAdapter`

The only project allowed to expose concrete Roslyn/MSBuild types to the rest of
the implementation.

Responsibilities:

- compiler/Workspace compatibility facade;
- syntax, symbol, semantic-model, `IOperation`, and control-flow helpers;
- union capability detection;
- metadata-name adapters for OneOf, ErrorOr, CSharpFunctionalExtensions,
  LanguageExt, ValueOf, Vogen, dunet, Thinktecture, and project-owned result
  types;
- cancellation and analyzer exception capture;
- source-location normalization.

Pin Roslyn packages centrally. Qualification tests compile and execute every
public API CSharpAssay relies on.

### 7.4 `CsAssay.Analyzers`

A normal `DiagnosticAnalyzer` package:

- concurrent execution enabled after thread-safety tests;
- generated-code policy explicit per rule;
- semantic operations preferred over spelling;
- syntax rules reserved for genuinely syntactic obligations;
- diagnostics available in IDE and `dotnet build`;
- no network, process launch, mutable global state, or nondeterministic input.

Analyzer callbacks report diagnostics. They do not decide the authoritative
four-state verdict.

### 7.5 `CsAssay.CodeFixes`

Only local, semantics-preserving fixes:

- replace a mutable auto-property setter with `init` where constructor and
  assignment analysis proves compatibility;
- add `sealed` to a verified DU case;
- replace a raw mutable collection exposure with a read-only surface when the
  backing representation is already safe;
- add an omitted simple type arm to a switch using a reviewed template.

Do not auto-fix:

- exceptions into an arbitrary `Result`;
- a loop into LINQ;
- a class into a record;
- a public OneOf API into a native union;
- domain primitives into guessed value objects;
- serializer/EF models.

Detection does not imply a safe rewrite.

### 7.6 `CsAssay.Workspaces`

The authoritative runner loads `.sln`, `.slnx`, and `.csproj` using registered
MSBuild and `MSBuildWorkspace`.

It must preserve:

- project references;
- linked files;
- generated documents;
- analyzers and generators;
- conditional constants;
- nullable context;
- language version;
- every declared TFM;
- source-generator outputs;
- compiler diagnostics.

Every workspace failure becomes evidence. The runner never falls back to
regex-scanning files and calls the result complete.

### 7.7 `CsAssay.Runner`

Commands:

```text
cs-assay check <project-or-solution>
cs-assay verify <project-or-solution>
cs-assay doctor
cs-assay catalog [--profile compat|native]
cs-assay explain <rule-id>
cs-assay migrate --report <project-or-solution>
```

`check` may narrow files/projects for fast feedback and cannot release.
`verify` evaluates the complete configured graph.

### 7.8 `CsAssay.Reporting`

Outputs:

- deterministic JSON;
- SARIF 2.1.0;
- concise console;
- optional static HTML generated solely from the JSON artifact.

Serialization is a projection of the domain model. Reporters do not recompute
severity, status, suppression, or verdict.

---

## 8. Analysis strategy

### 8.1 Syntax is used for

- `#nullable disable` directives;
- null-forgiving operators;
- setter/init shape;
- switch form and discard arms;
- empty catch blocks;
- `async void`;
- loop and statement-shape suggestions;
- suppression trivia and attributes.

### 8.2 Symbols are used for

- record/class/struct identity;
- mutability of exposed member types;
- inheritance and constructor accessibility;
- exact package/type recognition by metadata name;
- one-method interface shape;
- public domain-boundary signatures;
- source-generator output attribution;
- native/custom union identity.

### 8.3 `IOperation` and control-flow graphs are used for

- actual mutation calls rather than method-name strings;
- dominance of `IsTn`/`TryPickTn` checks before `AsTn`;
- thrown/caught exception flow in the restricted deterministic subset;
- blocking async calls;
- accumulator-loop recognition;
- result values ignored or collapsed unsafely.

No admitted semantic rule is keyed only by `ToString()`, source substring, or a
method's short name.

### 8.4 Project analysis is used for

- nullable and warning policy;
- all-TFM completeness;
- package and analyzer identity;
- architecture boundaries;
- public API use of OneOf/ValueOf;
- domain primitive glossary;
- generated code;
- suppressions and baselines;
- test and build evidence.

---

## 9. Rule catalogue strategy

Do not begin with a large aspirational rule count. Begin with a small trust
slice, prove it, then admit one rule at a time.

### 9.1 Categories

| Prefix | Category |
| --- | --- |
| `CSAN` | nullability and totality |
| `CSAI` | immutability and collections |
| `CSAU` | unions, results, options, and exhaustiveness |
| `CSAE` | errors, resources, and effect boundaries |
| `CSAF` | functions, expressions, and pipelines |
| `CSAD` | domain modeling |
| `CSAA` | async and concurrency |
| `CSAP` | policy, suppression, and project evidence |

### 9.2 Trust-slice candidates

The exact IDs are frozen in Phase 0 after collision and documentation review.

| Candidate | Mechanism | Initial certainty | Initial disposition |
| --- | --- | --- | --- |
| nullable disabled in a configured core project/file | project + syntax | deterministic | block |
| unauthorized null-forgiving operator in configured core | syntax + policy | contextual until policy supplies allowed boundaries | advise/inconclusive |
| public mutable setter on a configured immutable data type | symbol | deterministic within explicit scope | block |
| mutable collection exposed by an immutable carrier | symbol | deterministic for known types | block |
| externally extensible “closed” record hierarchy | symbols/constructors | deterministic for declared closed model | block |
| missing simple case in a proven compat closed hierarchy switch | semantic switch model | deterministic only for restricted patterns | block |
| discard arm on native union switch | semantic union identity | deterministic | warn/block by profile |
| unguarded OneOf `AsTn` extraction | operation + CFG | deterministic only for proven dominance subset | block |
| swallowed exception | operation + syntax | deterministic for empty/no-observable handling subset | block |
| `async void` outside recognized event handler | symbol/operation | deterministic | block |
| blocking `.Result`/`.Wait()` in async flow | symbol/operation | deterministic subset | block |
| unapproved CSharpAssay suppression | project/suppressed diagnostics | deterministic | block |

Null safety is the first offence family. The implementation additionally freezes:

| Candidate | Mechanism | Initial certainty | Initial disposition |
| --- | --- | --- | --- |
| null-forgiving operator in configured core | syntax + boundary policy | deterministic | block |
| null literal/reference `default` introduced as core data | operation + boundary policy | deterministic, excluding null pattern checks | block |
| nullable public contract in configured core | symbol nullability, including nested type arguments | deterministic | block |

Shell adapters may receive and test null only long enough to convert it into an
explicit option/result/domain case. A boundary designation changes scope; it
does not erase the finding or authorize null to flow into the core.

### 9.3 Contextual rules

These are useful but do not block without explicit policy facts:

- throwing for an expected domain failure;
- primitive obsession;
- a class that should be a record;
- a one-method interface that should be a delegate;
- a builder that should be an initializer/`with`;
- bool flags that encode mutually exclusive states;
- returning `null` where `Option` communicates better;
- mutation that escaped the imperative shell;
- `TryPick` outside an alleged hot path.

### 9.4 Heuristic suggestions

Never block:

- `if` ladder could be a switch expression;
- loop could be a LINQ pipeline;
- method could be expression-bodied;
- local helper could be static;
- operation could use a collection expression;
- class could use a primary constructor.

These are style/refactoring hints, not functional correctness.

### 9.5 Delegation

Do not duplicate:

- C# compiler nullability and native union diagnostics;
- .NET SDK analyzers for security, API design, reliability, and performance;
- IDE code-style analyzers;
- formatter behavior;
- test-framework analyzers.

Optional external analyzers are admitted only after:

- exact version pinning;
- positive and negative corpus verification;
- crash behavior observation;
- suppression/reporting integration;
- license and maintenance review.

An external analyzer finding remains namespaced and governed by its declared
admission. CSharpAssay must not remap hundreds of foreign diagnostics to one
opaque rule ID.

---

## 10. Configuration and suppression

### 10.1 One strict policy file

Use `.csassay.json` with a published JSON Schema:

```json
{
  "$schema": "./eng/schemas/csassay.schema.json",
  "profile": "auto",
  "release": {
    "allowPreviewToolchain": false,
    "requiredTargetFrameworks": ["net10.0"]
  },
  "boundaries": {
    "coreNamespaces": ["Acme.Domain", "Acme.Application"],
    "shellNamespaces": ["Acme.Infrastructure", "Acme.Web"]
  },
  "representations": {
    "resultTypes": ["Acme.Functional.AssayResult`2"],
    "optionTypes": [],
    "closedTypes": []
  },
  "domainPrimitives": {
    "Acme.Domain.CustomerId": ["customerId"],
    "Acme.Domain.OrderId": ["orderId"]
  }
}
```

Unknown keys, invalid metadata names, invalid profiles, and malformed JSON fail
configuration loading. Never silently fall back to defaults.

Roslyn analyzer options flow through `.editorconfig`/`.globalconfig` for IDE
behavior. The runner records effective values and checks them against the
authoritative policy. Configuration precedence is evidence, not trivia.

### 10.2 Suppression forms

Supported:

- a narrowly scoped `#pragma warning disable/restore`;
- `SuppressMessage` with required justification;
- a reviewed baseline entry with diagnostic fingerprint, owner, reason, and
  expiry;
- policy-declared generated or interop boundaries.

Rejected:

- repository-wide `NoWarn` for CSharpAssay diagnostics;
- wildcard CSharpAssay suppression;
- a baseline entry without expiry;
- “generated” based only on a path substring;
- suppression of a tool failure.

### 10.3 Generated code

Each document is classified from compiler/workspace evidence, generator
identity, attributes, and file metadata. Rules declare whether they apply to
generated code.

Generated code may be excluded from style advice, but:

- generator crashes are tool failures;
- generated public API may still participate in architecture and representation
  analysis;
- a hand-authored file cannot evade analysis by adding an `.g.cs` suffix;
- exclusions are listed in evidence.

---

## 11. Testing and adjudication

### 11.1 Test layers

1. pure domain/verdict property tests;
2. analyzer positive/negative/fix tests using current Roslyn testing packages;
3. malformed source and compiler-error tests;
4. workspace tests for solution/project/TFM behavior;
5. package adapter tests for every supported version range;
6. compatibility/native behavioral equivalence tests;
7. CLI exit-code and artifact golden tests;
8. analyzer crash and cancellation fault injection;
9. IDE latency/allocation benchmarks;
10. real-repository adjudication.

### 11.2 Analyzer test matrix

Every admitted rule covers:

- top-level statements;
- file-scoped and block namespaces;
- nested/local functions;
- generics;
- nullable enabled/disabled contexts;
- records, record structs, classes, and structs as applicable;
- generated source;
- syntax errors;
- multi-document compilation;
- `net10.0` and applicable `net11.0` preview/stable lanes;
- suppression and configuration precedence.

### 11.3 Functional laws

If CSharpAssay ships or recommends a stable result facade, test:

```text
Map identity
Map composition
Bind left identity
Bind right identity
Bind associativity
Match case preservation
MapError identity/composition
Traverse ordering
async cancellation preservation
```

Validation accumulation has separate applicative laws and must not be
implemented as fail-fast result with a misleading name.

### 11.4 Real-world adjudication

Start with consented repositories representing:

- ASP.NET APIs;
- EF Core domain models;
- workers/services;
- libraries;
- source-generator-heavy solutions;
- AOT/trimming;
- multi-targeting;
- UI/framework code;
- intentionally imperative high-performance code.

For every finding, record:

```text
true positive | false positive | contextual/needs policy | tool failure
```

No precision number is reported when its denominator is zero. Undefined is
`Inconclusive`, never 100%.

### 11.5 Performance budgets

Set budgets during Phase 0 from measurements, then gate regressions. Initial
targets, subject to evidence:

- analyzer incremental callback p95 below 50 ms on a representative document;
- full analyzer overhead below 10% of baseline compilation wall time on the
  qualification solution;
- bounded memory growth across repeated workspace verification;
- deterministic output across repeated runs and path relocation.

These are starting hypotheses, not promises.

---

## 12. Delivery phases

### Phase 0 — prove the platform and freeze the contract

Deliver:

- pinned stable .NET 10 SDK and isolated .NET 11 Preview 6 qualification lane;
- minimal solution/project layout;
- Roslyn analyzer “hello diagnostic” in IDE/build/test;
- MSBuildWorkspace loading for `.csproj`, `.sln`, and `.slnx`;
- all-TFM enumeration;
- compiler/analyzer crash capture;
- native union symbol/operation spike;
- OneOf, ErrorOr, CSharpFunctionalExtensions, LanguageExt, ValueOf, Vogen,
  dunet, and Thinktecture metadata/API matrix;
- strict `.csassay.json` parser and schema;
- verdict/evidence model;
- deterministic JSON/SARIF skeleton;
- final IDs and admission requirements for the trust slice.

Exit:

```text
no required Roslyn API is assumed;
native union detection works on the pinned preview or is explicitly marked unavailable;
failed project/analyzer evidence cannot become Pass;
the same fixture run twice produces byte-identical normalized JSON.
```

### Phase 1 — exercise and specimen corpus

Deliver:

- TG01-TG15 skeletons;
- compat bad/good corpus;
- native-preview bad/good corpus;
- fault corpus;
- behavioral golden tests;
- rule-to-specimen closure test;
- initial real-world repository set and adjudication format.

Exit:

```text
every trust-slice candidate has positive, negative, suppression, and fault evidence;
all exercises compile and are verified structurally;
no verifier relies on source substring matching.
```

### Phase 2 — analyzer trust slice

Stable-lane implementation status (2026-07-31): complete. Seven rules are
Admitted (`CSAP0001`, `CSAN0001`–`CSAN0004`, `CSAI0001`, and `CSAI0002`);
the remaining seven candidates in the Phase 2 catalogue stayed Prototype. The full solution produces an
authoritative Pass with complete evidence. Native-preview admission remains
unavailable because no .NET 11 SDK is installed.

Implement candidates in this order:

1. unauthorized suppression;
2. nullable disabled, null-forgiving, null introduction, and nullable public contracts in core;
3. immutable setter/collection holes;
4. swallowed exception and async-void restricted subsets;
5. compat closed-hierarchy shape;
6. compat simple switch completeness;
7. OneOf guarded extraction;
8. native union discard policy.

Each begins as `Prototype`. Promote one at a time only after admission.

Exit:

```text
at least six rules Admitted;
all admitted rules have real-world adjudication;
no analyzer crash is reported as zero findings;
IDE and build diagnostics agree for the same compilation.
```

### Phase 3 — authoritative CLI

Stable-lane implementation status (2026-07-31): complete. The five commands,
strict policy/profile negotiation, explicit project-reference/all-TFM
evidence, required-rule enforcement, policy-scoped xUnit v3 test execution,
four-state exits, and byte-deterministic JSON/SARIF are qualified on .NET 10.
Other test reporters and the native-preview lane remain explicitly
unqualified.

Deliver:

- `doctor`, `catalog`, `check`, `verify`, `explain`;
- project graph and all-TFM orchestration;
- strict policy/profile negotiation;
- compiler, analyzer, generated-code, suppression, and test evidence;
- four-state verdict and exit codes;
- deterministic JSON/SARIF;
- incremental `check` clearly separated from release `verify`.

Exit:

```text
compiler error => no Pass;
required TFM load failure => ToolFailure;
required rule skipped => Inconclusive;
admitted blocking finding => Fail;
clean complete evidence => Pass.
```

### Phase 4 — packaging and CI

Deliver:

- analyzer NuGet package;
- `dotnet tool` package;
- locked restore;
- reproducible/package provenance artifacts;
- signed releases if project policy supplies signing authority;
- GitHub Actions stable lane;
- separate non-authoritative preview lane;
- SARIF upload;
- installation and rollback docs.

Exit:

```text
fresh .NET 10 environment can install and verify a sample;
package contents contain no inspiration repository;
preview failure cannot block stable release unless explicitly promoted;
release artifacts match the tested hashes.
```

Implementation status on 2026-07-31: complete on the stable lane. The
repository now builds both packages twice, canonicalizes unsigned NuGet
archives, binds package metadata and provenance to the exact commit, compares
the results byte for byte, audits payload/signing state, and qualifies a fresh
tool and analyzer consumer from a local-only feed. GitHub Actions keeps the
stable release gate and non-authoritative preview probe in separate jobs,
uploads SARIF, and attests main-branch package bytes. Packages remain unsigned
because no NuGet signing certificate exists. Phase 6 adds a separately gated,
manual, passwordless NuGet trusted-publishing workflow.

### Phase 5 — migration report and ecosystem adapters

Deliver:

- `migrate --report`;
- public OneOf/ValueOf exposure inventory;
- compat/native switch and behavior comparison;
- boxing/layout warnings stated as risks, not benchmark results;
- serialization/EF/ASP.NET/AOT qualification adapters;
- Vogen/dunet/Thinktecture support only where Phase 0 evidence justifies it.

Exit:

```text
report makes no source change;
every recommendation links to the exact affected API and evidence;
no migration is described as find-and-replace.
```

Implementation status on 2026-07-31: complete on the stable lane. The
report-only command inventories public OneOf and ValueOf exposure through
nested generic, array, base-type, interface, member, and constraint surfaces.
Every exposure carries exact source, API, metadata/assembly, target-framework,
risk, compat/native comparison, adapter obligation, and recommendation
evidence. Framework adapters remain explicitly unqualified until their
executable corpus passes; Vogen, dunet, and Thinktecture support remains
disabled because Phase 0 established no qualified package version.

### Phase 6 — broader contextual guidance

Add advisory rules for:

- primitive obsession;
- strategies/builders/visitors that can be functions or patterns;
- state flags;
- loop-to-pipeline opportunities;
- exceptions at core boundaries;
- mutable shell leakage.

These remain advisory until a project policy supplies deterministic context.

Implementation status on 2026-07-31: complete on the stable lane. Six new
prototype advisory diagnostics cover the listed families with deliberately
restricted shapes. `CSAD0001` emits only when the policy supplies an exact
domain-primitive glossary; every Phase 6 rule is contextual or heuristic,
`Advise`, and non-admitted. Consumer policy cannot promote one into release
authority. The semantic corpus includes positive, negative, suppression,
malformed, and analyzer-failure specimens for all six rules. Observe/core/strict
templates, an adoption guide, and a manual NuGet OIDC publication workflow
harden rollout. Authoritative self-assay passes while retaining its advisory
inventory as evidence rather than demanding cosmetic silence. The analyzer
package now carries a qualified transitive MSBuild target: ordinary builds fail
on admitted findings and reject silent analyzer/`NoWarn` bypasses. The
playground/badge, evidence-foundry, read-only MCP, container, and community
sequence is recorded in `docs/ecosystem.md`; MCP is not falsely claimed as a
`0.1.0` capability.

The local native probe now runs with .NET 11 SDK
`11.0.100-preview.6.26359.118`. The preview compiler exposes Roslyn 5.9 union
syntax and `ITypeSymbol.IsUnion`, while the published tool remains bound to the
public Roslyn 5.6 NuGet graph. The solution builds cleanly under SDK 11, but the
native check correctly remains Inconclusive with 16 explicit gaps until that
dependency boundary is qualified.

---

## 13. Launch plan

### 13.1 Release stages

| Stage | Audience | Promise |
| --- | --- | --- |
| `0.1` research preview | contributors | platform spike, no release authority |
| `0.2` analyzer preview | selected repos | admitted trust slice, IDE/build diagnostics |
| `0.3` CLI preview | selected CI | four-state evidence, no broad compatibility promise |
| `0.5` public beta | external adopters | stable .NET 10 lane, documented false-positive process |
| `1.0` stable | production CI | pinned supported toolchain, admitted rules, deterministic evidence |
| native profile GA | opt-in then default-capable | only after .NET 11 GA qualification and ecosystem round trips |

Do not tie CSharpAssay 1.0 to .NET 11. The stable .NET 10 lane can ship first.

### 13.2 Usage journey

```text
dotnet tool install CsAssay.Tool
cs-assay doctor
cs-assay catalog --profile compat
cs-assay check MySolution.slnx
cs-assay verify MySolution.slnx --json artifacts/csassay.json --sarif artifacts/csassay.sarif
```

Adoption profiles:

- `observe`: diagnostics and evidence, never blocks;
- `core`: admitted deterministic rules block only in declared functional core;
- `strict`: broader deterministic set plus suppression gate;
- `migration`: inventories compat/native representation risk.

Teams start with `observe`, adjudicate, declare boundaries, then promote to
`core`. CSharpAssay does not force a strict profile on a legacy repository.

### 13.3 Documentation

Ship:

- five-minute setup;
- what a pass does and does not mean;
- compat/native decision guide;
- result versus validation versus option guide;
- imperative-shell boundary examples;
- each rule's mechanism, certainty, false-positive boundary, positive/negative
  specimens, suppression form, and fix safety;
- troubleshooting for MSBuild, generators, TFMs, and preview SDKs;
- migration and rollback guidance.

---

## 14. Risks and blind spots

| Risk | Current confidence | Control |
| --- | --- | --- |
| native union syntax/symbol model changes before GA | medium | isolated adapter, separate preview CI, no stable-lane dependency |
| semantic FP judgments produce false positives | medium-low | deterministic/contextual/heuristic separation; explicit core boundaries |
| OneOf/ValueOf assumptions drift | medium | metadata/version qualification; no runtime dependency in analyzer core |
| source-generator/IDE differences | medium | generated-document and design-time-build tests |
| all-TFM Workspace fidelity | medium | Phase 0 spike and fault corpus |
| analyzer performance harms IDE | medium | callback budgets, concurrency tests, benchmarks |
| suppressions disappear through EditorConfig precedence | medium | report suppressed diagnostics, policy audit, effective-config evidence |
| native union serialization/API shape changes | medium-low | golden round trips; native remains preview |
| default record structs permit invalid default values | high certainty risk | rule/docs distinguish convenience from proof; generator adapters tested |
| framework-required mutation creates noise | high certainty risk | shell/framework profiles and namespace/type exclusions with visible evidence |
| public migration breaks binary/wire contracts | high certainty risk | report-only migration and compatibility tests |
| “functional” becomes aesthetic dogma | high certainty risk | only mechanical obligations block; loops/classes/mutation allowed with scope/reason |
| current environment lacks a .NET SDK | known blocker for implementation tests | Phase 0 begins in an SDK-equipped environment/CI |
| inspiration tree contains dirty line-ending noise | known | treat it as read-only and exclude it from deliverables |

### Confidence gates

Current planning confidence:

```text
product thesis       9.4 / 10
development approach 8.6 / 10
launch path          7.2 / 10
usage/adoption       6.8 / 10
```

Raise development confidence above 9 only after Phase 0. Raise usage confidence
above 8 only after at least three materially different real repositories
complete adjudication with acceptable precision and IDE overhead.

---

## 15. Decisions still to make

These are Phase 0 evidence questions, not blockers to starting:

1. Does the stable result facade add enough value to justify a runtime package,
   or should CSharpAssay remain analyzer/CLI only?
2. Which maintained value-object generator, if any, earns first-class adapter
   status?
3. Can compat hierarchy exhaustiveness be proven for a useful restricted model
   without a CSharpAssay marker attribute?
4. What is the exact public Roslyn symbol/operation surface for preview union
   declarations on the pinned SDK?
5. Which test runner is authoritative for the initial repository?
6. Which real repositories can be used for consented adjudication?
7. What namespaces/projects constitute the first functional core?
8. Is `CsAssay` the permanent diagnostic prefix, or should the organization
   reserve another ID range before public release?

Recommended defaults:

- analyzer/CLI only for MVP;
- native record/class wrappers for simple value objects;
- OneOf supported but not required;
- Vogen and Thinktecture evaluated before ValueOf is recommended for new code;
- xUnit v3 or NUnit on Microsoft.Testing.Platform, selected once and executed
  through one authoritative CI command;
- `CsAssay` assemblies and category-prefixed IDs such as `CSAN0001`;
- Apache-2.0 to match the sibling project, subject to repository-owner approval.

---

## 16. Acceptance gate

CSharpAssay 1.0 is releasable only when all of the following hold in one clean
CI run:

```text
G1  stable .NET 10 toolchain and packages are locked
G2  every admitted rule has positive, negative, suppression, fault, and real-world evidence
G3  compiler/analyzer/project failure cannot produce Pass
G4  all required projects and TFMs are evaluated
G5  JSON and SARIF are deterministic and schema-valid
G6  suppressions and generated-code exclusions are visible
G7  IDE, build, and CLI diagnostic identities agree
G8  performance budgets pass
G9  installation works in a fresh environment
G10 the tool passes its own admitted rules or records reviewed boundary exceptions
G11 stable release does not depend on the .NET 11 preview lane
G12 claims in README and rule docs are generated from or checked against the catalogue
```

The native profile becomes stable only when:

```text
N1  .NET 11 is GA and supported
N2  final union syntax and Roslyn APIs are pinned
N3  native compiler exhaustiveness/nullability behavior passes the corpus
N4  JSON, ASP.NET, OpenAPI, EF/value objects, AOT, and multi-TFM tests pass where claimed
N5  compat/native behavioral golden tests pass
N6  migration documentation states every public/binary/wire break found
```

Maintain an obligation ledger:

```text
gate | commit | command | result | evidence artifact hash | timestamp
```

No gate is complete without its ledger entry and artifact.

---

## 17. Research register

Primary/current references:

- [FsAssay sibling project](https://github.com/CanonFlowFoundation/FSharpAssay)
- [Functional C# guidance](https://github.com/ArunNotFound/functional-skills/tree/main/functional-csharp)
- [Functional Programming in C# example/exercise code](https://github.com/la-yumba/functional-csharp-code-2)
- [.NET 11 Preview 6 announcement](https://devblogs.microsoft.com/dotnet/dotnet-11-preview-6/)
- [.NET 11 download and support posture](https://dotnet.microsoft.com/en-us/download/dotnet/11.0)
- [C# union type reference](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/builtin-types/union)
- [Roslyn language feature status](https://github.com/dotnet/roslyn/blob/main/docs/Language%20Feature%20Status.md)
- [.NET support policy](https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core)
- [C# using-alias constraints](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/using-directive)
- [Roslyn overview](https://github.com/dotnet/roslyn/blob/main/docs/wiki/Roslyn-Overview.md)
- [Analyzer configuration files](https://learn.microsoft.com/en-us/dotnet/fundamentals/code-analysis/configuration-files)
- [OneOf NuGet](https://www.nuget.org/packages/OneOf/)
- [ValueOf NuGet](https://www.nuget.org/packages/ValueOf/)
- [ErrorOr repository](https://github.com/amantinband/error-or)
- [CSharpFunctionalExtensions repository](https://github.com/vkhorikov/CSharpFunctionalExtensions)
- [LanguageExt repository](https://github.com/louthy/language-ext)
- [Vogen documentation](https://stevedunn.github.io/Vogen/)
- [dunet repository](https://github.com/domn1995/dunet)
- [Thinktecture Runtime Extensions repository](https://github.com/PawelGerr/Thinktecture.Runtime.Extensions)

Local research inputs are intentionally not packaged or copied into the
deliverable. Their licenses and provenance must be reviewed before any code is
adapted.
