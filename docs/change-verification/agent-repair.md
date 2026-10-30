# Repair a failed configured verification

1. Read the failed run's JSON, readable report and applicable repository
   instructions. Keep the failed artifacts and process exit.
2. Identify the exact admitted rule, application test or required evidence item.
   Distinguish a policy failure, incomplete evidence and a tool/setup failure.
3. Inspect affected code and relevant callers/state transitions, including
   unchanged source. For a threshold inspect both boundary values and its
   normalized units; for a state transition inspect prior and resulting states.
4. Make the smallest justified source repair. Preview a suggested IDE action and
   check callers/contracts before applying it. A setter repair need not preserve
   every caller and does not imply deep immutability.
5. Run a focused check if useful for diagnosis. It is not the final authority.
6. Run full configured authoritative `verify` on the revised source, with its
   required tests and fresh output paths. Confirm the recorded input, mode,
   authority, verdict, required evidence and execution diagnostics.
7. Report changed files, exact commands, observed outcomes and remaining
   uncertainty. A passing result means these configured requirements were met
   for this evaluated input; it does not prove complete application correctness.

Never move code into shell, widen exemptions, lower test minima, disable checks,
add unauthorized grants, delete tests, weaken assertions, adjust expected values
to a defect, remove required evidence or reuse stale artifacts to obtain Pass.
Do not conceal relevant failure history by retrying until one attempt passes.
If the business expectation is unclear, state the specific missing decision.
Separate proposed policy/test-expectation changes from an implementation repair.

The compiler supplies language/type checks. CSAssay supplies admitted configured
structural/evidence checks. Application tests supply encoded behavior under the
exercised conditions. AI suggestions remain advice until an executable check
confirms the behavior; prose/confidence cannot grant release authority.

## Freshness and existing consumers

CSAssay reports source hashes but has no general receipt-reuse authorization API.
Do not use an earlier receipt to authorize revised source/configuration. Rerun.
The Playground's existing `DistributionReports.BoundSummary` rejects incompatible
source/receipt bindings; `DistributionBoundaryTests.Stale_binding_cannot_generate_a_current_pass`
covers source commit, receipt digest and sourceUnchanged. Its code/fixture mapping
checks also reject changed bound inputs. Run these existing consumers as part of
this change's compatibility checks; do not claim their narrow contract validates
every application/environment dependency.

The three ExpenseGuard exercises record stage-specific file inventories, actual
commands and fresh verifier outputs, and restore all original input hashes.
Sequential checks do not prove concurrent payment safety or a live provider's
behavior. No runtime agent, signing service, cross-PR analysis or second evidence
protocol is part of this increment.
