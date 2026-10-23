# C# depth undertaking

This undertaking starts from published main `7be53b7`. New work is local and
stops at Forgejo draft review. Other language assays are deferred.

The first analyzer expansion is CSAU0004 catch-all coverage. It recognizes `var`,
parenthesized discard, `object` (including empty property patterns), untyped
empty property patterns and `not null`, including bounded `or`/`and` composition.
Explicit native cases remain accepted. Compiler diagnostics are checked before
using every specimen: invalid C# is not detector evidence. The original
detector missed ten valid new catch-all controls; their failing run is retained
under ignored `artifacts/depth/red-native-corrected.log`.

CSAU0004 remains Prototype. The seven admitted rules, native compiler authority,
exact SDK, nullable mandate and unchanged 50 ms performance budget remain intact.
The analyzer does not prove arbitrary predicates, interface coverage or guards.

Qualification must run from a clean commit with `./eng/qualify-local --output
artifacts/csharp-tooling-qualified`. Its seven-gate receipt is the authority for
the changed source, packages and performance results. Focused test success alone
does not qualify a release. Historical receipts retain their original identities.

The paired Playground undertaking adds typed preview evidence states and checks
their propagation through DAA. Independent human rule/case adjudication, live
provider quality, Windows support and production operational qualification remain
open. They cannot be completed by relabeling agent-authored local tests.
