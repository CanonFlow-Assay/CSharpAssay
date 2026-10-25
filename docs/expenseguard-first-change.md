# First change: an ExpenseGuard receipt threshold

This exercise uses the real Playground sample at
`CSharpAssay.Playground/samples/100-expenseguard`. Its application-defect fixes
are a separate workstream; this tutorial does not turn them into analyzer rules.
Work in a clean disposable branch/worktree so you can restore just your change.

## Prepare the current native development tool

Follow [local-feed adoption](adoption.md) in the engine checkout. In the sibling
Playground checkout, prepare its local packages using the actual engine path:

```bash
./eng/prepare-assay.sh ../CSharpAssay
```

This builds local development dependencies and may update local package-lock
identities. Inspect them separately; commit the preparation state before a
source-bound qualification. Do not discard unrelated edits or pretend a
previous receipt qualifies a new package build. Both checkouts require their
exact pinned .NET 11 RC2 SDK; no .NET 10 or union-library fallback is supported.

## Locate the rule and its executable requirements

From the Playground root inspect:

* `samples/100-expenseguard/src/ExpenseGuard.Domain/Model.cs`: `Policies.Demo`,
  whose receipt threshold is 25 base-currency units.
* `samples/100-expenseguard/src/ExpenseGuard.Domain/PolicyEngine.cs`: the check
  against `policy.ReceiptThreshold`; FX normalization happens first.
* `samples/100-expenseguard/tests/ExpenseGuard.Tests/PolicyTests.cs`:
  `Exact_thresholds`, including 25 and 25.01.
* `samples/100-expenseguard/tests/ExpenseGuard.Tests/TransitionTests.cs`:
  `Missing_receipt_cannot_reach_approved`.
* `samples/100-expenseguard/.csassay.json`: required tests, minimum and boundaries.

For this deliberate failing experiment, change only the receipt threshold in
`Policies.Demo` from `25m` to `30m`. Do not edit tests, grants or boundaries to
hide the disagreement. The unchanged requirements should reject the new behavior.

## Execute the actual required tests

The sample requires a real temporary PostgreSQL instance. With Docker available,
run from the Playground root:

```bash
dotnet build samples/100-expenseguard/ExpenseGuard.slnx -c Release
./eng/tooling with-inventory-database -- \
  dotnet samples/100-expenseguard/tests/ExpenseGuard.Tests/bin/Release/net11.0/ExpenseGuard.Tests.dll \
  --report-xunit --report-xunit-filename tests.xml \
  --results-directory evidence/generated/expenseguard/first-change-tests
```

Read the failing threshold assertions and retained test XML; never count a
compilation, unavailable database or incomplete execution as a detected business
mutation. Existing receipt/itemization/FX requirements can make other cases fail
as well. No live AI request or real payment is part of this exercise.

## Read an authoritative outcome

Set `ASSAY_RUNNER` to the actual qualified engine runner path (or use the freshly
installed local tool's absolute path instead). On the deliberately changed code:

```bash
export ASSAY_RUNNER="$PWD/../CSharpAssay/src/CsAssay.Runner/bin/Release/net11.0/cs-assay.dll"
./eng/tooling with-inventory-database -- dotnet "$ASSAY_RUNNER" verify \
  samples/100-expenseguard/ExpenseGuard.slnx \
  --policy samples/100-expenseguard/.csassay.json \
  --json evidence/generated/expenseguard/first-change.json \
  --sarif evidence/generated/expenseguard/first-change.sarif \
  --html evidence/generated/expenseguard/first-change.html \
  --markdown evidence/generated/expenseguard/first-change.md
```

A completed required-test failure is Fail/1. Open the HTML or read the Markdown
headline, required test rows and diagnostics. Missing evidence is incomplete;
execution/output failure is not a policy failure. The report explains the
actual outcome, not a guarantee that every expense rule is correct.

## Restore the experiment

Review `git diff -- samples/100-expenseguard/src/ExpenseGuard.Domain/Model.cs`.
In your clean disposable exercise, restore **only** the threshold edit with
`git restore -- samples/100-expenseguard/src/ExpenseGuard.Domain/Model.cs` and
rerun the same tests/verification into a fresh output directory. Do not overwrite
the failed evidence or restore over someone else's changes.

CSharpAssay executes admitted checks and requires configured evidence. The C#
compiler handles native-union exhaustiveness. The application tests enforce the
receipt policy. An explicit IDE set-to-init action for CSAI0001 is useful for an
immutability finding; it does not repair or infer the threshold requirement.
