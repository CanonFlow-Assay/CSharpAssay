# Repair evidence in existing reports

This increment retains JSON schema `1.2.0`, all established fields, their meanings,
verdicts and exits. It adds optional data; consumers must ignore unknown fields.
No new runtime package, evidence database or verdict evaluator is introduced.

## Additive fields

Each `evidence.tests[]` now includes `failureDetails` (an array of `testName` and
`message`) and `failureDetailsTruncated` (boolean). Old receipts without these
fields remain valid for existing consumers; absence means detail is unavailable,
not that tests passed. Aggregate `total`, `passed`, `failed`, `skipped`, `outcome`
and `exitCode` keep their meanings and remain complete when details are bounded.

Only validated individual failed TRX results contribute detail. The runner
preserves at most twenty failed results per required input, test names up to
1,024 UTF-16 units and messages up to 4,096 units, without splitting a surrogate
pair. Truncation of either list or text is explicit. No stack traces, stdout,
stderr, environment variables, run users or unrelated logs are copied. Missing
messages stay empty in JSON and explicitly unavailable in readable output.
Expected/actual values are shown only when supplied in that assertion message;
the verifier does not parse, infer or invent them. Tests must not put credentials
or unrelated private data in assertion messages. This is not a general redaction
or log-ingestion service.

When the CLI completes a verification, its completed result carries invocation
context for HTML/Markdown presentation: working directory, executable and
argument vector. This is intentionally excluded from canonical JSON/SARIF:
output destinations and transient tool-install paths must not change canonical
machine evidence for the same evaluation. The exercises separately retain exact
structured process commands in `commands.json`. No invocation schema extension
is introduced. Direct SDK results may lack this presentation context; readable
reports say unknown.

Arguments are data, not an executable shell string. Use a process argument API
or quote each argument for your shell; do not paste a JSON array as a shell
command. Invocation does not establish the input's current contents, installed
tool integrity or environment freshness.

JSON and the existing `assess.receipt` envelope retain structured detail.
Console and SARIF's existing required-test execution notification use the same
sanitized message; HTML/Markdown use the existing shared model and escaping.
No test failure becomes an analyzer finding. Report writers consume the completed
verdict and cannot turn missing evidence into Pass. Existing atomic publication,
requested-output failures and fingerprint v1 are unchanged.

## Reuse and compatibility checks

`TestExecutorTests` covers supplied/absent messages, ignores passing/unrelated
output, and checks count/name/message bounds. `ReadableReportTests` checks legacy
schema/count/outcome projection, assess envelope, shared console/SARIF detail,
escaped HTML/Markdown, unavailable detail and recorded CLI arguments in readable output. An actual repeated CLI check to different JSON paths verifies byte stability. Existing
seven outcome fixtures, mixed findings, suppressions, hostile text and atomic
output regressions remain. The Playground's existing consumers and three actual
package-installed exercises are the integration/compatibility check.

A source hash is evidence only for its declared file scope. The Git head alone
does not identify a dirty working tree. The exercise separately records a full
fixture input inventory at every stage and compares it before/after execution;
this does not add a universal signing, cross-repository or source-authorization
protocol to CSAssay.
