# Agent self-adjudication, 22 October 2026

This is an agent review of the engine and its evidence boundaries. It is not
independent human approval or production qualification. The starting revision
was `8fa9bbd32a8fffed77a072e56bd5bc4472b3a7c1`, previously qualified and merged
on local Forgejo. GitHub publication is outside this undertaking.

## Finding and correction

**SA-E01, high: incomplete TRX evidence could authorize Pass.** The TRX adapter
read only total/passed/failed/skipped counters. It did not reconcile individual
executions, duplicate execution identities, runner error counters or a successful
process exit accompanying failed tests. A skipped-only or empty selection could
be projected as Passed. Separately, the verdict factory accepted contradictory
caller-supplied test records without invalidating authority.

Eight new adapter cases and four domain cases reproduced these weaknesses before
the correction. Raw failing reports are retained under
`artifacts/self-adjudication/red-workspaces/` and `red-domain/`. The adapter now
requires individual executions to agree with counters, refuses duplicate or
missing execution identities and runner errors, and preserves zero/skipped
selections as NotRun. The domain layer independently marks incomplete required
test records as missing evidence, preventing authoritative Pass. A real failed
test retains Fail precedence; incomplete evidence cannot acquire authority.

The existing mixed-result parser fixture now includes the individual results
that substantiate its summary. No rule, nullable policy, boundary or test minimum
was weakened. The admitted rule set is unchanged.

**SA-X01, medium: expected qualification validation errors aborted the CLI.**
The qualification command's handled exception set omitted InvalidDataException.
Its dirty/unqualified-source guard stopped reuse correctly, but the process
aborted instead of returning its documented failure exit. A process-level test
uses an isolated local Git fixture, without remote access, to reproduce this
path. Handle the exception explicitly and retain exit 2 with its diagnostic.
The paired coordinator had the same gap; its corresponding test also requires
a structured JSON failure. These two CLI cases bring the audit's total to 20
new regression cases across both repositories.

## Verification contract

Focused red/green checks are retained under `artifacts/self-adjudication/`.
The exact clean fix commit must also pass all seven release qualification gates
using `eng/qualify-local --output artifacts/csharp-tooling-qualified` before its
packages are reused. The resulting JSON receipt records the actual source,
gate/log identities, package qualification and unchanged performance budget.
An incomplete receipt is not a completed qualification.

The first corrected-engine qualification passed 154 tests and all seven gates.
That report and its packages are retained in `engine-first-qualified/` and
`engine-first-packages/` under the audit artifacts before the final CLI fix is
qualified. They do not qualify the later source commit.

The prior qualification and package bytes were preserved outside both Git
repositories at
`../brainstorm/assay-reimagined/self-adjudication-before-20261022/` before the
new qualification. The paired Playground audit covers application manifests,
compiled application identity and per-scenario trace completeness.

## Remaining boundaries

Self-assay establishes compliance with the declared policy and tested negative
controls. It does not establish universal functional correctness, arbitrary
legacy support, independently adjudicated rule quality, Windows-host support or
production readiness. Engineering, host and integration projects remain explicit
shell boundaries; core classification was not expanded to hide a finding.
