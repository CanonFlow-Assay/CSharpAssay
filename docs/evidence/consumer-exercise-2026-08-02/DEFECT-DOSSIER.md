# CSharpAssay 0.1.1 Consumer Defect Dossier

Status: no-code dossier; human-reviewed consumer evidence accepted
Prepared: 2026-08-02 UTC
Target evidence: NCalc `52eeec5b4bea5dd3b8ae592f5070c6854100d8df`
Published product: `CsAssay.Tool` and `CsAssay.Analyzers` 0.1.1
Proposed patch baseline, if separately approved: CSharpAssay `main` at
`93ba007c83a14ef40184878cb73dc01233e67316`

This dossier records defects; it does not authorize implementation, a branch,
a pull request, a package, a tag, or publication. The accepted consumer
conclusion remains **use manually for experiments only**.

## Preserved evidence

The immutable hash manifest is [`ACCEPTED-EVIDENCE.sha256`](ACCEPTED-EVIDENCE.sha256).
It records the accepted report, all 1,444 adjudication rows, the 439 grouped
shapes, rejected refactor diff, both deterministic observation pairs, and
strict verification JSON/SARIF. The large report artifacts and CSV corpus are
intentionally not committed to CSharpAssay; the manifest preserves their
accepted identities without importing unrelated consumer data.

At capture time, the NCalc tracked tree had no diff and remained detached at
the accepted commit. NCalc is not a fixture or dependency of this patch.

## Severity summary

| ID | Severity | Area | Release significance |
|---|---|---|---|
| CSA-RA-001 | Critical | Authority / required tests | Release-authority blocker |
| CSA-RA-002 | Critical | Workspace/compiler completeness | Release-authority blocker |
| CSA-CLI-003 | Moderate | `--help` and `-h` | Consumer usability defect |
| CSA-CLI-004 | Moderate | `explain` documentation link | Consumer navigation defect |
| CSA-BUILD-005 | Major | Rollback with warnings-as-errors | Operational safety defect |
| CSA-BOUNDARY-006 | Major | CLI/analyzer boundary alignment | Adoption/enforcement safety defect |

The finding count is deliberately not a success metric. The 1,444 occurrences
demonstrate multi-TFM expansion, policy mismatch, and the need for human-owned
scope decisions.

## CSA-RA-001 — Authority emitted with a required test NotRun

### Expected invariant

`authoritative` may be true only when every required test has actually run,
its reporter is qualified, its count meets policy, and its result is recorded.
A required test with `outcome=notRun` can never contribute authority.

### Actual evidence

In `artifacts/csassay-verify.json`:

- top-level `verdict` is `fail`, exit code is 1;
- `evidence.authoritative` is `true`;
- required test `test/NCalc.Tests/NCalc.Tests.csproj` has `outcome=notRun`;
- its recorded exit code is -1 and totals are all zero;
- `evidence.failures` is empty;
- no missing-evidence item names the unsupported TUnit reporter or test
  execution component.

The public installation contract says unqualified test stacks produce
ToolFailure rather than silent authority. NCalc uses TUnit on Microsoft Testing
Platform, not the qualified xUnit-v3 reporter.

### Risk

An AI agent can observe `authoritative=true` and `Fail` and incorrectly treat
the finding set as a complete release decision. The individually accurate
`notRun` field is too remote to make that combination safe.

### Required correction

1. Compute authority from evidence completeness, never from command selection.
2. If a required reporter is unsupported or cannot execute, set
   `authoritative=false`, identify the component, and return ToolFailure.
3. If a required test executes and fails, authority may remain true because the
   failure is complete evidence; the verdict remains Fail.
4. Add a machine-enforced invariant forbidding `authoritative=true` alongside
   any required test outcome other than a completed, qualified result.

### Acceptance evidence

- unsupported required TUnit fixture: ToolFailure/exit 3,
  `authoritative=false`, `notRun`, component and actionable message present;
- qualified passing fixture: authority true only after counts are captured;
- qualified failing fixture: authority true, Fail/exit 1, real counts captured;
- deterministic JSON/SARIF on repeated executions.

## CSA-RA-002 — Workspace compiler gaps do not prevent authority

### Expected invariant

Authority requires complete compilations for every required project/TFM.
Workspace compiler errors, unloaded generated members, or incomplete semantic
models must force `authoritative=false`. Rules must not be described as
completed for scopes that were not semantically complete.

### Actual evidence

NCalc's untouched baseline Release build exits 0 and all 708 tests pass.
CSharpAssay's workspace evidence nevertheless records:

- 74 compiler errors across nine project/TFM instances;
- unresolved members such as `BinaryOperators` and generated `TypeHelper`
  members;
- nine `CSASSAY-COMPILER-ERRORS` missing-evidence entries;
- all 44 project/TFM records marked `loaded=true`;
- all 19 applicable aggregate rule outcomes marked `completed`;
- strict verification marked `authoritative=true`.

This is a workspace/source-generator model disagreement, not an NCalc compiler
defect. The product records the disagreement but does not let it invalidate
authority or completion labels.

### Risk

Zero findings for any affected rule could be mistaken for a completed clean
analysis. Existing findings also cannot be assumed complete. Deterministic
bytes make the incomplete result reproducible, not authoritative.

### Required correction

1. Any error-severity workspace compiler diagnostic in required scope makes
   authority false.
2. Distinguish a target that genuinely does not build from a CSharpAssay
   workspace/generator-host disagreement:
   - confirmed target compilation failure: Inconclusive/exit 2;
   - normal build succeeds but CSharpAssay workspace is incomplete, or the
     workspace host/generator cannot model it: ToolFailure/exit 3.
3. Introduce an explicit `incomplete` rule outcome (or an equally unambiguous
   schema representation) whenever any required project/TFM for that rule lacks
   complete compilation evidence.
4. Retain SDK, MSBuild, Roslyn, project, TFM, source-generator, and compiler
   diagnostic identities on the failure path.
5. Verdict precedence must prevent blocking findings from masking incomplete
   authority: ToolFailure, then Inconclusive, then complete Fail, then Pass.

### Acceptance evidence

- generator-backed fixture whose normal build passes but workspace compilation
  lacks generated members: authority false, ToolFailure, rules incomplete;
- genuinely broken repository fixture: authority false, Inconclusive;
- complete fixture with a blocking finding: authority true, Fail;
- complete clean fixture: authority true, Pass;
- no rule reports completed for an incomplete required project/TFM.

## CSA-CLI-003 — `--help` and `-h` are not product help aliases

### Actual evidence

- `dotnet cs-assay --help` and `dotnet cs-assay -h` emit `Unknown command`,
  render help on stderr, and exit 64;
- `dotnet tool run cs-assay --help` is intercepted by the .NET tool host and
  renders host help, not CSharpAssay help;
- `dotnet tool run cs-assay help` correctly renders product help once on stdout
  and exits 0.

### Required correction

- direct executable `cs-assay --help` and `cs-assay -h` must behave exactly
  like `cs-assay help`: stdout once, empty stderr, exit 0;
- consumer docs must avoid the .NET host interception ambiguity and show a
  known forwarding-safe/direct invocation;
- unknown commands must continue to exit 64 with a clear error.

## CSA-CLI-004 — `explain` emits repository-relative documentation

### Actual evidence

`explain CSAN0001` and `explain CSAF0001` exit 0 and give useful metadata, but
end with `docs/rules/<ID>.md`. That path is not usable in an unrelated consumer
repository. Analyzer and SARIF diagnostics already emit working GitHub HTTPS
help links.

### Required correction

- reuse the canonical diagnostic help-link mapping; do not add competing URL
  construction;
- known rules must emit complete HTTPS URLs, including CSAN0001 and CSAN0004;
- unknown IDs must retain their clear nonzero behavior.

## CSA-BUILD-005 — Documented rollback fails under consumer WAE

### Actual evidence

With NCalc's repository-wide `TreatWarningsAsErrors=true`,
`CsAssayEnforceOnBuild=false` disables the explicit CSharpAssay target gate but
the ordinary compiler still promotes admitted analyzer warnings to errors.
The build exits 1. Adding consumer `TreatWarningsAsErrors=false` makes the build
exit 0 with all 28 findings preserved as warnings.

### Required correction

- the documented rollback property must neutralize only CSharpAssay's admitted
  warning promotion even when the consumer globally treats warnings as errors;
- findings must remain visible as warnings/notes and evidence must not be
  erased;
- other compiler/analyzer warnings must retain the consumer's policy;
- anti-bypass rejection remains active whenever enforcement is true;
- documentation must call rollback an audited enforcement-disabled state, not
  a pass.

## CSA-BOUNDARY-006 — CLI policy and analyzer enforcement disagree

### Actual evidence

The strict CLI policy correctly demotes admitted findings in shell projects to
advice. The analyzer package enforces at installation/project scope and does
not consume the same boundary decision. Marking `NCalc.Domain.csproj` as shell
in `.csassay.json` still produces 28 analyzer build errors.

### Risk

A central/transitive analyzer installation can block serializers, tests,
generators, framework adapters, and compatibility shells that the reviewed CLI
policy deliberately excludes from core enforcement.

### Required correction

- define one policy projection for project identity and Core/Shell role, reused
  by CLI and analyzer build integration;
- no independent competing boundary interpretation;
- a missing, malformed, or unmappable policy must be explicit rather than
  silently treating a reviewed shell as core;
- no-policy behavior must remain documented and backward-compatible;
- central-package and transitive-package fixtures must prove shell projects do
  not become blocking while core projects still do.

## Cross-cutting non-goals

- no new rules, analyzer heuristics, code fixes, migration features, or roadmap;
- no reduction or suppression of the accepted 1,444 findings to claim success;
- no change to advisory-as-nonmandatory semantics;
- no conversion of TUnit to xUnit and no fabricated test evidence;
- no weakening of compiler, analyzer, anti-bypass, TLS, or package checks;
- no automatic refactoring based solely on a CSharpAssay count.

## Human decision required

Approval of this dossier accepts the problem statement only. Implementation
may begin only after separate approval of the accompanying patch-release plan,
