# Functional C# Profile — Shape v0.1

Status: normative construction contract  
Profile version: `shape-v0.1`  
Qualified CSharpAssay release: `0.1.2`

## 1. Purpose and product boundary

Shape v0.1 defines a reviewed starting architecture for a small greenfield .NET
application. It is guidance for humans and AI agents plus a contract for the
reference evidence in CSharpAssay.Playground.

`shape-v0.1` is **not** a new CSharpAssay CLI profile. CSharpAssay 0.1.2 accepts
the executable profiles `auto`, `compat`, and `native`; the Shape reference
uses a normal `.csassay.json` policy with `"profile": "compat"`.

Shape v0.1 does not add or claim:

- a `dotnet new` template or a published `CsAssay.Templates` package;
- new analyzers, diagnostic rules, dispositions, CLI commands, or options;
- automatic OOP conversion, architectural rewriting, or business correctness;
- EF Core, messaging, security, concurrency, or performance qualification;
- an authoritative LLM verdict.

CSharpAssay 0.1.2 is already published. Its admitted rule and evidence contract
remains unchanged and is the only CSharpAssay behavior this profile uses.

## 2. Normative language

`MUST` and `MUST NOT` are acceptance requirements for the reference shape.
`SHOULD` records the preferred construction rule; an exception requires written
human adjudication. `MAY` is optional.

Each requirement below identifies its actual enforcement owner. A principle is
not tool-enforced merely because it is desirable.

## 3. Project roles and dependency direction

The reference solution has four production roles:

```text
Domain <- Application <- API
   ^           ^          |
   |           |          |
   +----- Infrastructure -+
```

The arrows point from a consumer to a referenced core dependency in prose:

- `Domain` MUST reference no Application, API, Infrastructure, ASP.NET,
  persistence, serialization, dependency-injection, clock, randomness,
  filesystem, network, or messaging assembly.
- `Application` MUST reference Domain only. It is framework-free core
  orchestration. It MAY define ports that describe required effects, but it
  MUST NOT contain concrete I/O implementations.
- `Infrastructure` is an imperative shell. It MAY reference Application and
  Domain to implement Application-owned ports.
- `API` is the composition and transport shell. It MAY reference Application,
  Infrastructure, and Domain solely to translate transport input, invoke a
  workflow, and translate the explicit outcome.
- Domain and Application MUST NOT reference either shell project.
- Shell DTOs MUST be converted once at the boundary; Domain types MUST NOT be
  serialized as API contracts in Shape v0.1.

Dependency direction is enforced by architecture tests and human review, not
by a new CSharpAssay rule.

## 4. Approved closed representations

The reference uses no third-party union dependency.

### `Result<TValue,TError>`

`Result<TValue,TError>` MUST be an abstract closed representation with exactly
two public cases:

- `Success`, carrying one non-null `TValue`;
- `Failure`, carrying one non-null `TError`.

The type parameters MUST have `notnull` constraints. Construction MUST reject
null case payloads. Consumers MUST handle both cases explicitly. `default` is
not an approved domain outcome and MUST NOT be introduced as a substitute for
either case.

Expected validation or business rejection MUST use `Failure`; exceptions are
reserved for unexpected infrastructure or programming failures.

### `Option<T>`

`Option<T>` MUST be an abstract closed representation with exactly two public
cases:

- `Some`, carrying one non-null `T`;
- `None`, carrying no payload.

`T` MUST have a `notnull` constraint. `Some(null)` and a nullable payload on
either case are prohibited. The singleton-like absence case is `None`; `null`
is not a third case.

The reference behavior tests and architecture tests own these representation
requirements. CSharpAssay representation metadata supplies analysis context;
it does not prove all runtime or exhaustive-handling semantics.

## 5. Domain and workflow rules

### Domain

The Domain project:

- MUST contain immutable, constructor-complete values and decisions;
- MUST receive all information required for a decision as explicit input;
- MUST return explicit data or `Result`, never perform an effect;
- MUST use `Option` for modeled absence rather than nullable domain state;
- MUST NOT read time, randomness, environment, storage, network, or static
  mutable state;
- MUST NOT throw for an expected validation or business rejection;
- MUST NOT expose externally mutable collections.

### Application

The Application project:

- MUST orchestrate Domain decisions through Application-owned ports;
- MUST keep transport and infrastructure types outside its public workflow
  contract;
- MUST perform no concrete I/O;
- MUST preserve the distinction between expected Domain failure and unexpected
  port/infrastructure failure;
- MUST invoke an effect zero times after Domain failure and exactly once after
  a successful decision in the reference workflow.

### API and Infrastructure

The imperative shell:

- MUST validate and convert external representation once at the boundary;
- MUST map both Result cases to stable transport responses;
- MUST own concrete effects and composition;
- MUST NOT reinterpret an expected Domain failure as an exception;
- MAY use ordinary OOP, ASP.NET, DI, and mutable implementation details when
  they remain contained by the shell boundary.

## 6. Enforcement and evidence ownership

| Requirement | CSharpAssay 0.1.2 | Other authority |
| --- | --- | --- |
| Nullable analysis remains enabled | `CSAN0001` admitted | compiler |
| No null-forgiving shortcut in scoped core | `CSAN0002` admitted | review |
| No introduced null value in scoped core | `CSAN0003` admitted | behavior tests |
| No nullable public core contract | `CSAN0004` admitted | compiler and tests |
| Immutable carrier setters | `CSAI0001` admitted | architecture tests |
| No mutable collection exposure | `CSAI0002` admitted | behavior tests |
| No unauthorized suppression | `CSAP0001` admitted | policy review |
| Closed Result and Option cases | representation metadata is context only | architecture tests and review |
| Domain/Application dependency direction | not enforced by an admitted rule | negative architecture tests |
| No hidden Domain effects | selected evidence only; no universal claim | architecture tests and review |
| Expected failure remains explicit | `CSAE0002` is advisory only | behavior tests and review |
| State and functional-style suggestions | prototype/advisory only | human adjudication |
| Business correctness | not claimed | product-owner tests and review |

The seven admitted rules MAY block only inside the reviewed analyzer scope.
Prototype and advisory findings MUST be recorded and adjudicated; zero advisory
findings is not required.

The analyzer package MUST be referenced directly and privately only by the
Domain and Application projects. It MUST NOT be installed centrally or
transitively across API, Infrastructure, tests, framework, serializer, or
generated-code shells. CSharpAssay 0.1.x CLI boundary policy does not configure
the analyzer's project-level build scope.

## 7. Required 0.1.2 policy shape

The reference `.csassay.json` MUST:

- use executable profile `compat`;
- declare Domain and Application as core projects/namespaces;
- declare API and Infrastructure as shell projects/namespaces;
- register the metadata names for `Result`, `Option`, and their closed cases;
- require exactly the seven admitted rule IDs;
- configure the qualified xUnit v3/Microsoft Testing Platform test project,
  Release configuration, and a stable minimum test count;
- contain no suppression unless it has fingerprint, owner, reason, and expiry.

Policy scope can make a CLI finding nonblocking in the shell. It cannot demote
an analyzer diagnostic in a project that directly references the analyzer.

## 8. Real consumer commands

Generated instructions and evidence MUST use only commands shipped by
CSharpAssay 0.1.2:

```text
dotnet tool restore
dotnet tool run cs-assay doctor
dotnet restore Shape.slnx --locked-mode
dotnet build Shape.slnx --no-restore --configuration Release
dotnet test Shape.slnx --no-build --no-restore --configuration Release

dotnet tool run cs-assay check Shape.slnx \
  --policy .csassay.json \
  --json artifacts/check.json \
  --sarif artifacts/check.sarif \
  --html artifacts/check.html

dotnet tool run cs-assay verify Shape.slnx \
  --policy .csassay.json \
  --json artifacts/verify.json \
  --sarif artifacts/verify.sarif \
  --html artifacts/verify.html
```

`observe`, `fix`, `--strict`, `--out-json`, `--out-sarif`, and
`--profile functional-core-recommended` are not 0.1.2 interfaces and MUST NOT
appear in executable Shape v0.1 instructions.

## 9. Deterministic acceptance gate

The reference event is accepted only when:

1. locked restore and Release build succeed from a clean checkout;
2. all behavior and architecture tests execute and pass;
3. no required project is missing and no workspace/compiler error remains;
4. all seven admitted rules complete with zero blocking findings in scoped
   core;
5. every advisory finding is retained and adjudicated;
6. configured tests meet the exact minimum and CSharpAssay reports
   `authoritative: true`, no missing evidence, and no tool failure;
7. JSON and SARIF from two no-source-change runs are byte-identical;
8. source, policy, package, and evidence hashes are recorded;
9. negative boundary and authority mutations fail for the intended reason;
10. no analyzer, rule, CLI, profile, package, or release surface changed.

HTML is a human review artifact. Shape v0.1 does not claim byte-deterministic
HTML unless separately demonstrated.

## 10. AI-agent contract

The executable reference MUST include an `AGENTS.md` that states:

- the dependency direction and approved Result/Option cases;
- real build, test, check, and verify commands;
- prohibited null, suppression, analyzer-disable, policy-weakening, direct
  Domain effect, and shell-to-core leakage shortcuts;
- that an advisory requires human judgment rather than automatic repair;
- that `Inconclusive`, `ToolFailure`, skipped/zero tests, unloaded projects,
  compiler errors, stale evidence, or changed artifact counts are not success;
- the human stop points for public representation or policy changes.

An LLM may build, test, explain, or judge a bounded change. Its judgment is
always advisory. Deterministic compiler, test, CSharpAssay, provenance, and
hash evidence outrank it; disagreement is reported, never converted to Pass.

## 11. Independent evaluation event

The builder exercise MUST add one bounded business behavior without changing
the approved representations, dependency graph, policy strength, or shipped
CSharpAssay behavior.

An independent tester MUST then exercise, in disposable copies:

- zero or filtered required tests;
- a missing required project;
- a compiler/workspace error;
- analyzer disable, `NoWarn`, and unreviewed warning demotion;
- a forbidden Domain dependency or direct shell/framework reference;
- a direct time/random/storage/network effect in Domain;
- direct Domain serialization or transport leakage;
- stale JSON/SARIF after a source change.

Each mutation MUST fail deterministically or be recorded as an explicit
coverage gap. Mutation source MUST NOT be retained in the accepted reference.

The final LLM judge receives the contract and complete deterministic evidence.
It may assess clarity and agent safety, but it cannot override any failed gate.

## 12. Known limitations and next gate

Shape v0.1 proves one reference architecture under one qualified toolchain and
test lane. It does not prove EF, messaging, generated-code, serialization
round-trip, distributed failure, concurrency, performance, general user
comprehension, or organization-wide adoption.

The reference is hand-authored evidence, not a template product. Template
extraction, additional guards, code fixes, EF/messaging examples, or executable
functional profile names require separate proposals and qualification after
human review of this milestone.

Only a human may approve and merge the contract and reference pull requests.
