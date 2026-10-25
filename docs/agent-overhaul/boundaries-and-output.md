# Policy boundaries and standard output

This is a selected functional C# policy, not a general purity proof. Native
.NET 11 unions still require preview language. See the separate SDK prerequisite
and the final qualification index for actual executed hosts and source commits.

## Shared policy and host inputs

The verifier and packaged analyzer use the same `PolicyParser` and normalized
`PolicyEvaluation`. Pure decisions receive project/namespace identity and one
UTC evaluation date; they do not read files, clocks or Roslyn state. The verifier
loads and hashes one policy snapshot per run. Build/editor hosts consume declared
`AdditionalFiles` and compiler-visible `CsAssayPolicyPath`, project name/path,
TFM and `CsAssayEnforceOnBuild`. The package discovers the nearest ancestor
`.csassay.json`, or accepts an explicit absolute `CsAssayPolicyPath`.

Project selectors are literal paths relative to the policy directory. Dot
segments normalize; parent-relative selectors identify only already evaluated
graph projects and never cause extra file access. Namespace identifiers support
Unicode and escaped keyword spelling while retaining ordinal case. Path
separators normalize; matching is ordinal on Linux and ordinal-ignore-case on
Windows. Namespace matching is ordinal and includes child namespaces. Exact
core/shell contradictions are invalid, including normalized project paths.
Nested namespace exceptions are supported: shell namespace wins within a
selected core project. Shell project wins before namespace classification.
Unknown namespaces are not guessed from file names.

The existing default remains: without explicit selectors the default scope uses
core disposition; projects outside a nonempty `coreProjects` list remain default
advisory. With explicit core namespaces, unmatched namespace scope remains default
advisory. A shell namespace alone now takes effect even without core namespaces;
the old verifier accidentally ignored it in that case. Evidence reports `core`,
`shell` or `default`, never silently renames unselected default scope to shell.
CSAP0001 always blocks. Disabled nullable flow produces an integrity finding and
missing compiler evidence, even within shell or an accepted grant.

Solution verification validates selectors against its evaluated project graph;
unmatched project selectors make that scope incomplete. A project-only run
reports unmatched selectors as observations because other solution projects were
not evaluated. Unused namespace observations explicitly cover only inspected
compilations/configurations, not every conditional configuration or reference.
The packaged analyzer sees a compilation, not a complete solution graph. Its
scope is build/editor finding policy, not full release-scope verification.

A declared missing, unreadable or malformed policy fails closed. The CLI retains
configuration failure evidence; the analyzer emits non-suppressible CSAP0001.
An absent policy in provisional analysis retains the documented built-in default;
authoritative verification still requires policy.

## Grants and identity migration

Grants require a known rule, one lowercase SHA-256 finding fingerprint, nonblank
owner and concrete reason, and ISO expiry date. Duplicate/conflicting identities
and CSAP0001 grants are rejected. Identity comparisons are ordinal. Expiry is
inclusive against one UTC date captured at run/compilation start. Tests inject
the date/clock. Owner strings declare policy ownership; they do not authenticate
human approval. No blanket ignore attribute is added.

A matching policy-file grant retains the finding as an accepted suppressed
observation. Supported pragma/SuppressMessage syntax also requires matching
unexpired grants for every affected finding. Blanket/unmatched/self suppression
is an integrity failure. CSAP0001 cannot be downgraded by shell or editorconfig.
The package retains NoWarn, WarningsNotAsErrors and disabled-analyzer guards.
MSBuild booleans are case-insensitive; malformed enforcement values fail closed.
Supported per-rule/category/global editorconfig demotion attempts are integrity
errors. Explicit `CsAssayEnforceOnBuild=false` remains an **unqualified rollback**;
it is not evidence that enforcement ran. Hosts that remove an analyzer, disable
IDE analysis or control both source and enforcement configuration are outside
this guarantee. No independent IDE or Windows execution is claimed.

Fingerprint algorithm v1 is unchanged. Its canonical path input is now relative
to the policy directory, shared across verifier/build inputs. Existing JSON
`input`, project/source paths, raw finding severity and verdict meanings remain
unchanged. When the input directory differs from policy directory, old fingerprints
must be reviewed/regenerated; do not reuse stale grants. Additive `identityPath`,
`scope`, policy `textSha256`/`evaluationDate` and suppression identity/owner/date
explain the decision. Policy `sha256` still hashes original bytes; text SHA hashes
the decoded analyzer snapshot and can differ for BOM/encoding/unsaved IDE text.

Use the generated EF `documented-real-grant.json` or fresh-package
`accepted-policy.json` with its corresponding receipt for the executable example.
These use actual generated finding identities, not fabricated fingerprint text.

## Console, reports and release authority

`--format msbuild` is the default; `--format text` retains the former text layout
without its implicit 50-finding limit. `--show-suppressed` prints detailed accepted
observations. Otherwise all active findings print and accepted totals remain.
Admitted deterministic blockers are `error`; selected admitted shell findings are
`warning`; prototypes are `note`. Accepted suppressions remain `note` observations
and never active errors. Qualified descriptors cannot have their severity changed
by compiler configuration; fresh-package controls require the shell warning to
remain visible and nonblocking under global warnings-as-errors.
Console and SARIF share this projection, while raw JSON severity stays unchanged.

Locations use one-based line/column positions. Spaces, Unicode, parentheses and
drive paths are retained/escaped as appropriate. Console messages and paths strip
control/newline/directional controls, so they cannot inject diagnostic lines.
No terminal styling is emitted. Diagnostics and final summaries go to stdout;
infrastructure details go to stderr. Provisional `check` says **Analysis only**,
including when its existing exit-zero semantics apply. Exit zero alone cannot
authorize release: consume complete verify/assess scope and authority.

Exit codes stay 0 configured checks passed/provisional semantics, 1 policy or
completed required-test failure, 2 incomplete/unsupported evidence, 3 execution,
cancellation or requested-output failure. CLI usage errors remain 64.

Requested JSON/SARIF/HTML bytes are staged first, then replaced atomically **per
file** on the tested filesystem. Earlier valid bytes survive staging failure.
Cancellation/write failure returns 3 and prints no success summary. This is not
a multi-file transaction: if a later rename fails, already replaced files can
remain. No SARIF artifact is fabricated when that requested file cannot be written.

SARIF 2.1.0 uses the official approved-errata schema. Standard invocations retain
CLI exit code/description and execution success. Completed policy/required-test
failures execute successfully; missing scope, interruption and tool failure do
not. Successful provisional execution remains explicitly non-authoritative.
Configuration/execution notifications survive zero-result incomplete runs.
Accepted policy grants are external, source suppressions inSource, with accepted
status/justification and fingerprint/rule linkage. URI paths escape spaces and
Unicode. CLI output supplies the actual input source-root mapping; library-only
calls without a physical root use the documented virtual `csassay-source:/`
base. Physical external paths are represented relative to that supplied root.
No optional timestamps or machine inventory are added to SARIF.

New diagnostic codes allocated by this overhaul:

| Code | Meaning |
| --- | --- |
| CSASSAY-BUILD-GATE-CONFIG | Malformed build enforcement boolean |
| CSASSAY-BOUNDARY-PROJECT-UNMATCHED | Selector absent from evaluated solution graph |
| CSASSAY-NULLABLE-FLOW-INCOMPLETE | Disabled nullable analysis leaves compiler evidence missing |
| CSASSAY-REQUIRED-TESTS-FAILED | Completed required tests failed (presentation/notification) |
| CSASSAY-CANCELLED | Interrupted analysis or requested output |
| CSASSAY-ANALYSIS-FAILED | Unexpected analysis execution failure |
| CSASSAY-REPORT-WRITE | Requested report staging/replacement failed |

These are execution/configuration codes, not new admitted functional rules. The
seven admitted rules and prototype admission remain unchanged. The additive verificationMode preserves requested verification versus analysis
even when authority is lost to incomplete scope. Existing machine
consumers ignore additive fields; paired application qualification exercises the
actual assess consumer and preserves result, scope and release authority.

## Readable summaries (developer-experience increment)

`check`, `verify` and `assess` now accept optional `--markdown path`, alongside
existing JSON/SARIF/HTML outputs. HTML and Markdown share a report projection of
the completed in-memory verdict and `DiagnosticPresentation`; no second scan or
policy evaluation runs. They show Analysis only / Verification passed / failed /
incomplete / Tool failed, requested input, mode, authority, effective categories,
required tests, missing evidence, diagnostics and recorded identities near the
start. Default-scope warnings are distinct from shell warnings. Prototype raw
catalogue disposition cannot manufacture effective blockers; accepted suppressed
observations remain visible separately.

No tests configured is explicitly different from Not run. When policy
configuration failed, absence of test results is not relabeled none configured.
Zero findings with missing evidence remains incomplete. Input/scope and identity
fields are the recorded evidence, not fresh host guesses; contextual doctor is
available when current prerequisite facts are needed.

Paths/messages/other source text are escaped for HTML/Markdown and control/line
breaks are sanitized. Generated documentation links come only from canonical
catalogue HTTPS URLs; artifact paths are listed as text. The summaries include
no active script or full raw JSON envelope. Identical recorded evidence produces
deterministic summaries. All requested outputs use the existing per-file atomic
writer. A requested Markdown failure returns3 and prints no success summary;
this still is not a multi-file transaction.

Existing JSON/assessment fields, verdicts, exit codes, SARIF fields and fingerprint
v1 meanings are unchanged. Human HTML layout is replaced; text console headings
now also use the shared honest headline. No new identity scheme, tracking
baseline, grant database or new rule is introduced.

See [the CI summary example](../../eng/templates/ci-summary.yml). It uses this
repository's real required TypeGym policy and a freshly installed local tool.
Reports use a fresh per-run directory. Verification's original exit status is
preserved while always-steps retain only produced files and copy actual Markdown
to GITHUB_STEP_SUMMARY. No previous artifact or fabricated empty report is used.
Optional SARIF upload needs host availability/permissions and does not grant Pass.
