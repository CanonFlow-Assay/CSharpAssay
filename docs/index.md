# CSharpAssay

CSharpAssay 0.1.2 is a published C# design-assessment and CI enforcement tool
with reproducible evidence. It identifies selected non-functional design risks
and guides human-controlled refinement. It is not an automatic functional-C#
converter or a correctness proof system.

## Quick start

Pin the command-line tool in your repository:

```text
dotnet new tool-manifest
dotnet tool install CsAssay.Tool --version 0.1.2
dotnet tool restore
dotnet tool run cs-assay doctor
```

Add the analyzer package privately to a project:

```text
dotnet add package CsAssay.Analyzers --version 0.1.2
```

Read [installation and rollback](installation.html) before making CSharpAssay a
required CI gate.

## Documentation

- [Installation and rollback](installation.html)
- [Staged adoption](adoption.html)
- [Functional C# Profile — Shape v0.1](FUNCTIONAL-CSHARP-PROFILE.html)
- [Migration inventory](migration.html)
- [Ecosystem roadmap](ecosystem.html)
- [Release qualification](release.html)
- [NuGet publishing](publishing.html)
- [Reproducible Playground evidence](https://canonflow-assay.github.io/CSharpAssay.Playground/)

## Rule catalogue

The seven admitted rules can participate in an authoritative release verdict.
The remaining rules are prototype or prototype-advisory evidence and cannot
block a 0.1.2 release verdict.

### Admitted

- [CSAI0001 — Mutable setter on immutable carrier](rules/CSAI0001.html)
- [CSAI0002 — Mutable collection exposure](rules/CSAI0002.html)
- [CSAN0001 — Nullable analysis disabled](rules/CSAN0001.html)
- [CSAN0002 — Null-forgiving operator](rules/CSAN0002.html)
- [CSAN0003 — Null value introduction](rules/CSAN0003.html)
- [CSAN0004 — Nullable public core contract](rules/CSAN0004.html)
- [CSAP0001 — Unauthorized suppression](rules/CSAP0001.html)

### Prototype and advisory

- [CSAA0001 — Async void](rules/CSAA0001.html)
- [CSAA0002 — Blocking async flow](rules/CSAA0002.html)
- [CSAD0001 — Configured primitive obsession](rules/CSAD0001.html)
- [CSAD0002 — State flags](rules/CSAD0002.html)
- [CSAE0001 — Swallowed exception](rules/CSAE0001.html)
- [CSAE0002 — Expected failure at a public boundary](rules/CSAE0002.html)
- [CSAF0001 — Behavior-only type candidate](rules/CSAF0001.html)
- [CSAF0002 — Loop-to-pipeline opportunity](rules/CSAF0002.html)
- [CSAI0003 — Mutable shell leakage](rules/CSAI0003.html)
- [CSAU0001 — Extensible configured hierarchy](rules/CSAU0001.html)
- [CSAU0002 — Incomplete configured hierarchy switch](rules/CSAU0002.html)
- [CSAU0003 — Unguarded OneOf extraction](rules/CSAU0003.html)
- [CSAU0004 — Native union discard](rules/CSAU0004.html)

## Scope and limitations

CSharpAssay reports only what its admitted rule contract and supplied evidence
can establish. A clean result does not prove general correctness, architecture
quality, security, domain correctness, concurrency behavior, or performance.
Prototype and advisory findings require human adjudication. Missing project,
compiler, policy, target-framework, or configured-test evidence never becomes
a clean authoritative verdict.

Source code and issue tracking are available in the
[CSharpAssay repository](https://github.com/CanonFlow-Assay/CSharpAssay).
