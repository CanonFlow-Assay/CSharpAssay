# Adoption without surprise enforcement

CSharpAssay adoption is staged. A team should understand its evidence before
allowing any diagnostic to block a release.

## 1. Observe

Install exact version `0.1.1`, copy `eng/templates/observe.csassay.json`, and
run provisional observation:

```text
dotnet tool install --local CsAssay.Tool --version 0.1.1
dotnet tool run cs-assay doctor
dotnet tool run cs-assay check MySolution.slnx \
  --policy .csassay.json \
  --json artifacts/csassay-observe.json \
  --sarif artifacts/csassay-observe.sarif
```

`check` can exit zero while reporting findings. Inventory each rule, separate
core from framework shells, and record false-positive/context decisions.

## 2. Declare the core

Copy `eng/templates/core.csassay.json`. Replace every `Acme.*` example with
real projects and namespaces. Add domain glossary entries only after a domain
owner confirms the concept and expected type.

The six Phase 6 families (`CSAD0001`, `CSAD0002`, `CSAF0001`, `CSAF0002`,
`CSAE0002`, and `CSAI0003`) are contextual or heuristic advice. They cannot be
listed as required rules and cannot fail a release in `0.1.1`.

## 3. Establish authority

Copy `eng/templates/strict.csassay.json`, replace the sample paths, and add the
real test project and minimum stable test count. Run `verify` in a non-required
CI job first:

```text
dotnet tool run cs-assay verify MySolution.slnx \
  --policy .csassay.json \
  --json artifacts/csassay-verify.json \
  --sarif artifacts/csassay-verify.sarif
```

Require the job only after the project loads completely, every admitted rule
completes, configured tests run, and suppressions have reviewed fingerprints,
owners, reasons, and expiry dates.

The analyzer package also imports a transitive MSBuild gate. It defaults
`CsAssayEnforceOnBuild` to `true`, so ordinary `dotnet build` fails on admitted
blocking diagnostics. It also fails explicitly if `RunAnalyzers=false`,
`RunAnalyzersDuringBuild=false`, or `NoWarn` contains an admitted CSharpAssay
rule. This compiler-time gate is fast feedback; authoritative `verify` remains
the release gate because it also proves project graph, all target frameworks,
policy, configured tests, and evidence completeness.

## 4. Operate and roll back

Pin package and tool versions in source control. Upload JSON/SARIF even when a
job fails. Upgrade the analyzer and tool together, compare evidence before and
after, and retain the old package version for rollback.

Never promote a Phase 6 suggestion solely because the tool emitted it. Supply
the missing business/framework context, create a deterministic restricted
rule if one exists, qualify it independently, and only then consider admission.

For emergency rollback only, a reviewed build can set
`CsAssayEnforceOnBuild=false`; disabling enforcement should be reverted together
with the triggering package change and recorded in the audit trail.
