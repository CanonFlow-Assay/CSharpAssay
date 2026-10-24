# Retained overhaul qualification attempts

The independent SDK prerequisite (`3187106`) passed all seven engine gates on
.NET 11 RC2, with 170 tests and p95 33.4952 ms under the unchanged 50 ms budget.
Its paired SDK-only Playground qualification is a separate source/evidence scope.

The first overhaul candidate, engine `46bd574`, completed its seven-gate run with
unchanged source and **releaseQualified=false**. Restore, build, self-assay,
reproducible packages and package-profile boundaries passed. Tests and fresh
installation failed. All 203 tests ran: 199 passed, four failed, none skipped.
The actual p95 was **50.9247 ms**, with a 134.7828 ms maximum. These failures and
raw samples remain under the original `CSharpAssay-Overhaul` worktree; they are
not overwritten or relabeled successful.

Three functional failures and the fresh-install failure came from using the old
`BoundaryScope` specimen as a positive control even though it deliberately has
`Nullable=disable`. The new integrity requirement correctly prevents that from
passing through shell policy. The original specimen and disabled-nullability
counterexample are retained. Its integration test now explicitly requires
integrity failure and missing compiler-flow evidence. New `CompleteBoundaryScope`
provides a compiler-complete mutable shell record for exit-zero, required-test
not-run, prototype-admission and installed-tool positive controls. Test minima
and negative controls are not removed or weakened.

The follow-up candidate uses namespaces supplied by Roslyn callbacks instead of
creating/querying a semantic model per finding. Per-compilation project/date
facts and constant diagnostic descriptors are reused; diagnostic property maps
use a builder. No policy is cached globally or keyed only by compilation. The
performance test, workload, warmups, sample count and 50 ms budget are unchanged;
actual new measurements, not this optimization description, determine admission.

Additional package controls require ordinary visible shell warnings under global
warnings-as-errors and policy assignment in the consumer's project properties.
Compiler-protected effective descriptors retain selected warning severity;
prototypes and accepted suppressions remain nonblocking observations. Policy
AdditionalFiles are evaluated after consumer project properties. The copied
package counterexample is compiled by default so the installed CLI and build
inspect the same code, rather than supplying a build-only conditional property.

Project selectors normalize literal dot segments and can identify declared
loaded projects outside the policy directory. Selectors do not load extra files.
Namespace validation accepts Unicode identifiers and ordinal identity, including
escaped keyword spellings. External-policy consumer fixtures retain their test
minima and use correct policy-relative project selectors.

Final admission still requires clean committed source, all seven engine gates,
exact paired packages, all original fourteen Playground gates and the additional
EF boundary contract gate. Qualification receipts and their external review index
supply exact source IDs, hashes and executed outcomes. No claim of independent
human review, IDE/Windows execution, live provider quality or production approval
is made here.
