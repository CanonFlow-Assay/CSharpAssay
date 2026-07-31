# Real-world adjudication corpus

Repositories enter this set only with recorded consent and an immutable commit
identity. Every finding is classified as true positive, false positive,
contextual/needs policy, or tool failure. A rule cannot be admitted from rows
whose status is `not-run`.

The Phase 2 row uses the owner-provided `functional-csharp-code-2` corpus at
immutable commit `8d37775d80a71d09aa8ad8168168bd9c257f4980`. Five projects
loaded with no compiler errors, missing evidence, or tool failures. Of 56
admitted-rule findings, 22 were true positives, 34 were contextual boundary
findings, and none were false positives. Precision is reported only over the
22 adjudicated positive/false-positive findings; contextual rows are excluded.

Rules with no findings are recorded as `run-clean` with precision
`inconclusive`; the corpus does not invent a percentage for a zero
denominator.
