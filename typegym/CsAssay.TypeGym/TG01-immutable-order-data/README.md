# TG01 — Immutable order data

Primary obligation: Use records and immutable collections so a public carrier has no mutation hole.

`Bad.cs`, `Compat.cs`, and `Native.cs` are compiled independently by the
TypeGym harness. The harness executes `TypeGym.Challenge.Probe()`, compares
the semantic analyzer result with the checked-in expectation, and rejects
compiler errors or analyzer crashes. It does not inspect source substrings.

The native expectation is capability-bounded. Where the current SDK lacks the
qualified native-union surface, `Native.cs` is an executable unavailable-lane
marker rather than invented preview syntax.
