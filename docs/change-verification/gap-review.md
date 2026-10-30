# Change verification: increment 1 source review

30 October 2026. Local work only; no publication or rule admission.

## Inputs and baseline

The implementation brief is `/home/arun/Downloads/1.txt` (30 October).
Engine: `CSharpAssay-ChangeVerification`, branch `feat/change-verification`,
base `717d5d2daff548b580c651c7bc1247964bc826c2`.
Playground: `CSharpAssay.Playground-ChangeVerification`, same branch name,
base `b543ccedab4f2af8a565f37e68506a1dab486eae`.
Both isolated worktrees started clean. Existing publication worktrees and older
default branches remain untouched. These are locally recorded main revisions;
no new remote freshness check was made.

The named 25 October `CSharpAssay-Lean-Developer-Experience-Agent-Instructions.txt`
was not found by filename in Downloads, Documents or Developer. Consequently a
clause-by-clause reconciliation against that original text is **unverified**.
The existing implementation, its tests, `docs/adoption.md`,
`docs/expenseguard-first-change.md` and prior local publication record establish
the reuse below; they do not substitute for the missing original brief.

## Gap list against the available brief

| Requirement | Baseline status | Source / verification to retain or perform |
| --- | --- | --- |
| Existing check/verify/assess CLI; native policy | Implemented | `src/CsAssay.Runner/CommandLine.cs`, `CommandApp.cs`; `CommandLineTests`, integration tests. Verify/assess execute required tests; check is analysis only. |
| Practical rule explanations and docs | Implemented | `CommandApp.ExplainAsync`, `CsAssay.Catalogue/RuleCatalogue.cs`, `CsAssay.Runner/RuleDocumentation.cs`, `docs/rules/`; explanation and reporting tests. Reuse, no new command family. |
| Precise prerequisite diagnostics | Implemented | `src/CsAssay.Runner/DoctorCommand.cs`, `DoctorTests.cs`; checks exact SDK pin, runtime, MSBuild, Roslyn, native feature and policy. Doctor does not verify an application. |
| Shared effective readable reports | Implemented | `CsAssay.Reporting/DiagnosticPresentation.cs`, `ReadableReport.cs`, console/HTML/Markdown writers; `ReadableReportTests` covers pass/blocker/tests/missing/tool/analysis, mixed findings and hostile text. |
| Machine evidence, atomic output, compatibility | Implemented | JSON/SARIF/assessment writers, `AtomicReportWriter.cs`, `ReportingTests`, `AtomicReportTests`. Preserve existing fields and failures. |
| Boundaries, suppression authorization, fingerprint v1 | Implemented | `CsAssay.Workspaces/PolicyLoader.cs`, analyzer `AnalysisPolicyContext.cs`, domain verdicts, existing boundary/suppression/integrity fixtures. No new grants or scope exemptions. |
| ExpenseGuard required business behavior | Implemented | Playground `samples/100-expenseguard/tests/ExpenseGuard.Tests/PolicyTests.cs`; exact threshold cases 25/25.01 and unchanged assertions. Existing minimum 189, twelve mutations and real PostgreSQL remain. |
| A: compiling admitted fault + packaged authoritative verify + required tests + repair | Partial | Existing `ExpenseQualification.cs` mutable control rejects a build; `RepairExercise.cs` uses a separate structural specimen without application tests. Add an isolated declared core specimen beside ExpenseGuard, with its own compiler-only project and all existing application test requirements. Keep build analyzer enforcement in the real Domain project unchanged. |
| B: threshold fault + test attribution + repair | Partial | Existing missing-receipt mutant is verified; first-change tutorial uses a threshold edit. Add exact `>` to `>=` boundary replay through installed packaged CLI, unchanged tests and policy. |
| C: missing required evidence + fresh repair | Absent as an ExpenseGuard replay | Existing test executor and verdict factory classify skipped required tests as NotRun/incomplete. Temporarily skip one existing required test in an isolated copy, retain minimum and requirement, restore its original source. Missing input/no TRX remain tool failures. Confirm actual TRX behavior end to end. |
| Failed test identity and supplied assertion detail | Partial | `TestExecutor.ParseTrx` validates individual results then drops their detail; `TestRunEvidence` stores counts only. Add bounded names/messages from structured failed results only, no stdout/stderr/stack trace ingestion; no guessed expected/actual values. |
| Actual repeat command | Partial | CLI/documentation commands exist, but completed reports do not record invocation. Record CLI executable/argument vector/working directory as additive evidence, leave direct API invocations unknown. No shell quoting promises. |
| Source freshness and agent repair procedure | Partial | Source hashes already exist; Playground source/build consumers bind evidence. Document full-scope fresh reruns and test existing freshness rejection; do not create an agent runtime or new protocol. |
| Qualification/performance | Implemented, must rerun for changes | `eng/qualify-local`: seven gates, deterministic pack and fresh install. `docs/performance-profile.md`: unchanged forty-record workload, three warmups, twenty samples, strict 50 ms p95. Playground existing relevant tests plus three fresh packaged replays. |

## Three reviewable increments

1. This gap review records reuse, missing original brief and concrete gaps.
2. Isolated A/B/C replay and tutorial; retain failure reports, exact commands,
   per-stage source identities and restored verification. No defects in final app.
3. Minimal additive test failure/invocation reporting, compatibility regressions,
   agent procedure and final qualification report.

The compiler checks language/types; admitted CSAssay checks enforce configured
structure/evidence; application tests encode particular business expectations;
AI advice supplies no verification or release authority. None proves complete
application correctness. No FsAssay or other assay changes are included.

## Compatibility refinement during increment 3

The existing GoF, Shape and CSV scripts compare canonical JSON/SARIF bytes across
different output destinations. Exact invocation metadata therefore belongs in
HTML/Markdown presentation and the exercise command record, not canonical JSON.
The necessary machine extension remains bounded structured test failure detail.
An actual repeated CLI regression covers destination-independent machine bytes.
