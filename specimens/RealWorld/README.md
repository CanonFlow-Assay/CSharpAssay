# Real-world adjudication corpus

Repositories enter this set only with recorded consent and an immutable commit
identity. Every finding is classified as true positive, false positive,
contextual/needs policy, or tool failure. A rule cannot be admitted from rows
whose status is `not-run`.

The initial self-host row records the requested CSharpAssay implementation.
Its working tree is intentionally not counted as adjudicated evidence. The
summary denominator therefore remains zero and precision is `inconclusive`,
never an invented percentage.
