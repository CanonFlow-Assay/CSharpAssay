# Adoption and distribution roadmap

CSharpAssay should spread through a proof loop, not through claims of universal
code quality:

```text
install -> build fails on admitted evidence -> fix or reviewed suppression
        -> SARIF explains the decision -> public case study proves the workflow
```

The useful north-star metric is repositories retaining a green authoritative
gate after 30 days. Stars, chat membership, and raw download counts are only
supporting signals.

## Shipped foundation

- `CsAssay.Analyzers` makes admitted diagnostics visible in editors and builds.
- Its transitive MSBuild gate is enabled by default. `dotnet build` fails when
  an admitted blocking finding exists, analyzers are disabled, or `NoWarn`
  attempts to hide an admitted CSharpAssay rule.
- `CsAssay.Tool` produces deterministic JSON and SARIF with four-state verdicts.
- GitHub workflows qualify and attest packages; SARIF can feed code scanning.
- [CSharpAssay.Playground](https://github.com/CanonFlow-Assay/CSharpAssay.Playground)
  publishes controlled and pinned public before/after evidence.

## Next distribution slice

1. Publish the two pinned NuGet packages and a copy-paste GitHub Actions job.
2. Add a badge generated only from an authoritative report: verdict, tool
   version, admitted policy hash, and evidence workflow must all be identifiable.
3. Grow the playground from one kata to three bounded case studies with pinned
   revisions, licenses, characterization tests, human adjudication, and blind
   spots.
4. Add a read-only MCP server over the existing CLI contract. Initial tools
   should be `catalog`, `explain`, `check`, and `verify`; no rewrite tool should
   exist until behavioral qualification supports it.
5. Supply a Docker/dev-container image for demonstrations and reproducible bug
   reports. Normal .NET consumers should keep using NuGet and the local tool
   manifest rather than paying a container tax.

The MCP boundary must constrain inputs to an allowed repository root, disable
network access by default, preserve the CLI exit/verdict distinction, return
artifact paths plus structured summaries, enforce timeouts, and never turn
missing evidence into success. MCP is not claimed as shipped in `0.1.1`.

## Later, when demand proves it

- Visual Studio and Rider onboarding templates after analyzer behavior is
  stable across their compiler hosts.
- A custom .NET SDK only if the analyzer package plus transitive targets cannot
  express a demonstrated need. An SDK is a large compatibility surface, not a
  marketing checkbox.
- Discord after there is enough recurring user traffic to answer questions and
  moderate responsibly. Until then, GitHub Discussions and issue templates keep
  knowledge searchable and close to reproducible evidence.

## Evidence foundry, not a hall of shame

Public examples should credit maintainers and describe the assayed revision,
policy, findings, behavior tests, changes, remaining findings, and blind spots.
Never label a repository or its authors “bad.” A project may be correct for its
context while violating an intentionally narrow CSharpAssay policy.

Track time to first report, setup failure rate, false-positive adjudications,
authoritative-gate retention, package upgrades, and contributed case studies.
Those measurements reveal whether adoption is real; a star count does not.
