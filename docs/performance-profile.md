# Qualified analysis runtime profile

The CLI and representative-document performance fixture explicitly disable
tiered compilation. This selects optimized JIT code immediately, with a possible
startup-cost tradeoff. The analyzer NuGet package runs inside its consumer host;
it cannot impose this runtime setting on an editor or compiler process. See
[Microsoft's compilation configuration reference](https://learn.microsoft.com/en-us/dotnet/core/runtime-config/compilation).

The workload remains forty records, three warmups, twenty complete analyzer
runs, diagnostic determinism and the strict 50 ms p95 budget. No rule, diagnostic,
severity, test selection or timing threshold is reduced. Optional raw samples
are emitted when local qualification sets CSASSAY_PERFORMANCE_REPORT. The
local qualification record captures the source commit and every gate result.

Keep the runtime distinction visible: the default tiered profile previously
failed at 123.733 ms p95 in the complete local suite. A separate tiering-disabled
run passed both performance tests. Another exploratory optimized-profile run
under concurrent qualification measured 50.4693 ms and correctly failed; it is
not discarded or promoted to success. Current qualification requires a complete
fresh run. These measurements do not establish equal performance on every host
or a universal improvement in startup or application throughput.

Metadata identities are cached using weak symbol keys, retaining compiler-graph
collectibility and reference identity. Nullable traversal avoids constructing a
metadata name for non-generic types and only performs the Nullable identity
comparison for the relevant name. Dedicated regressions cover nested generic
names, distinct compilation scopes, nullable generic/value cases and mutable
aliases. The admission matrix and functional corpus remain mandatory.

Package audit requires the tool's actual runtime configuration to retain this
profile. Qualification also mutates real package copies to enable/remove it and
requires both to be rejected for the expected reason. None of these local steps
publishes a package or updates GitHub.

A single cold-process `doctor` diagnostic measured 0.724 seconds for the previous
default profile and 1.209 seconds for the optimized profile. This is one
observation per configuration, not a startup benchmark; the tradeoff remains
explicit rather than being hidden by the analyzer timing gate.

The isolated optimized-profile check with metadata caching measured 33.2646 ms
p95 and passed both performance tests; all 42 analyzer tests also passed. The
complete clean-commit qualification is recorded separately and remains the
release authority. The representative source, warmups, sample count and 50 ms
threshold are unchanged.
