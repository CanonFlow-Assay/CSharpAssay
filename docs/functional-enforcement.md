# Functional enforcement and its limits

The active toolchain is pinned .NET 11 RC1 and C# preview. The catalogue, compiler,
project policy and executed evidence jointly define the actual guarantees.

| Concern | Current authority |
| --- | --- |
| Native union syntax and exhaustiveness | Pinned C# compiler |
| Native-union discard/default guidance | CSAU0004 prototype; advisory, not admitted |
| Mutable record setters / known mutable collection exposure | CSAI0001 and CSAI0002, admitted |
| Disabled nullable analysis, null-forgiving, introduced null and nullable core APIs | CSAN0001–CSAN0004, admitted |
| Unauthorized suppression | CSAP0001, admitted |
| Domain effects, architecture direction and domain semantics | Selected analyzer guidance, declared architecture/behavior tests and review; no universal purity or correctness claim |
| Mutation, risk, wire compatibility and resilience previews | Scoped observations retained separately from admitted rule authority |
| DAA proposal | May select an eligible registered investigation; cannot execute arbitrary commands, waive a gate, exclude a survivor or grant Pass |

Only the seven catalogue-admitted rules can supply admitted blocking evidence.
No prototype is promoted by naming a functional profile or selecting native
unions. For a new application declare its domain/shell boundary, requirements,
expected public contract and executed test/worker obligations. Keep missing,
unsupported, stale and interrupted states visible. Absence of a finding does
not prove absence of all effects, defects or compatibility breaks.

The current [platform matrix](../eng/qualification/platform-matrix.md) distinguishes
implementation scope from release qualification. Historical admission evidence
retains its original SDK and source identity rather than being relabeled.
