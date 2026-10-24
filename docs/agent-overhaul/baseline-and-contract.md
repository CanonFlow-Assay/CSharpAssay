# Agent overhaul baseline and contract

Started from engine `ef9de10bc319c1814a6f2a1a71faaea9c58bf483` and
Playground `8fcb37d5b31e348255d452ce7fd611c410d517c7`, with clean source
worktrees. Work is isolated in `CSharpAssay-Overhaul` and
`CSharpAssay.Playground-Overhaul` on `feat/agent-overhaul`. The original
qualified worktrees remain preserved. One agent; local Forgejo review only;
GitHub publication requires the owner's subsequent review approval.

## Gaps observed before behavior changes

1. Boundary classification exists only in verifier-private code. The packaged
   analyzer receives representation options but no shared boundary/grant policy.
   Build targets promote every admitted rule irrespective of shell boundaries.
2. Core/shell declarations have no contradiction or evaluated-graph validation.
   Namespace and project matching need explicit host/path semantics. Policy
   loading failure must never become a permissive shell fallback.
3. Suppression grants have basic syntax checks but not complete known-rule,
   duplicate/conflict or self-suppression validation. Expiry reads the clock per
   finding. Current shell classification can demote CSAP0001: this must close.
4. Console output has no MSBuild format and an implicit 50-finding cap. It prints
   the analysis summary before requested outputs succeed. Requested files are
   overwritten directly rather than staged and replaced per file.
5. SARIF lacks invocation execution metadata/notifications, uses raw severity
   rather than shared effective presentation, and labels all suppressions
   inSource. URI/source-root handling needs explicit schema/semantic controls.
6. Existing verification/assessment consumers and fingerprints must remain
   auditable. Any canonical identity/path change requires migration notes,
   while existing JSON fields retain their meanings. Prototype rules remain
   nonblocking and the seven admitted rules remain unchanged.
7. There is no bounded EF boundary demonstration proving actual persistence,
   pure Domain dependency separation and failure retention under shell policy.

## SDK prerequisite, independent of output/policy work

The inspected checkouts pin SDK `11.0.100-rc.1.26425.128`. Microsoft's
[support policy](https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core)
lists RC1 support ending **2026-10-13**, with RC2 go-live support through
**2026-11-10**. RC1 baseline executions are historical comparison evidence,
not a claim of current supported-platform qualification.

The [official release metadata](https://builds.dotnet.microsoft.com/dotnet/release-metadata/11.0/releases.json)
selects SDK `11.0.100-rc.2.26504.105`. Its Linux archive SHA-512 is
`54e9463339337cef68f455e97dadac0a9e1b49416a3b15504b577144b24ae4b4eed4e7ba3bac507650b61edf704e977b4627d3804f4c2688cc5bd69b4f2eecdd`.
It is installed in an isolated cache, leaving the previous SDK untouched.
Native union compilation/exhaustiveness and preview-language status must be
probed separately; runtime go-live support does not qualify a language feature.
No .NET 8/10 lane, union polyfill/generator or .NET 12 implementation is added.

Record the toolchain-only change separately and qualify it before changing
policy enforcement or presentation. Preserve the unchanged performance budget,
test minima and negative controls. Baseline commands/results and later source
identities belong in ignored `artifacts/agent-overhaul/` and the paired
Playground evidence directories, with an immutable review index at completion.

## Acceptance and compatibility plan

Executed RC1 baseline: all **170** engine tests passed with no failures/skips;
p95 **34.7491 ms** against the unchanged **50 ms** budget (the raw 20 samples,
including a 117.3368 ms maximum, remain recorded). All **474** Playground tests
and eight upstream characterization checks passed after explicit baseline-only
package-lock preparation. Its two preparation commits change package identities,
not application behavior. Restore/build/test logs and performance JSON are kept
under `artifacts/agent-overhaul/baseline/`. These results do not extend RC1 support.

The isolated RC2 preview probe compiled and executed native `Outcome(Accepted,
Rejected)` conversion, matching and runtime IUnion identity, producing 42.
Non-preview language rejection and omitted-case rejection are separate probes;
their actual diagnostics are retained with the prerequisite evidence.

Register failing controls for packaged/verifier boundary parity, invalid policy,
grant validation/expiry, integrity findings in shell, build/editorconfig bypass,
unbounded MSBuild output, output failure/atomic replacement, SARIF execution and
suppression semantics, real consumer JSON compatibility and fresh packages.
Use the official SARIF 2.1.0 errata schema and separate semantic assertions.
Then add the real-provider EF example with a Domain dependency architecture test.
Console format changes deliberately; `--format text` retains the old layout.
Original verify/assess JSON fields, verdicts, source bindings and rule admission
remain intact. New classification/evaluation/presentation fields are additive.

## Separately registered source-review follow-ups

- Trace-transition concerns: source-review observation, not runtime-confirmed.
- Omitted-input-extension concerns: source-review observation, not runtime-confirmed.
- Stale no-build concerns: source-review observation, not runtime-confirmed.

Do not fold these into this overhaul unless an executed boundary/output case
requires it; record that scope change and its actual evidence if it occurs.
