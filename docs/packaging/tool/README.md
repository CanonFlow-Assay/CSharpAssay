# CSharpAssay tool

`CsAssay.Tool` installs the `cs-assay` command-line verifier.

```text
dotnet tool install --global CsAssay.Tool --version 0.1.2
cs-assay doctor
cs-assay --help
cs-assay explain CSAN0001
cs-assay check MySolution.slnx
cs-assay verify MySolution.slnx
cs-assay migrate --report MySolution.slnx --json migration.json
```

`check` is provisional. `verify` is authoritative and returns exit code 0
only when the configured compiler, analyzer, target-framework, rule, and test
evidence is complete and clean. See the repository documentation for full
installation, adoption, CI, evidence, migration, and rollback guidance.
`help`, `--help`, and `-h` render the same product help when invoking the tool
directly. `explain` emits the complete HTTPS documentation URL shared with
analyzer diagnostics and SARIF.
`migrate` is a report-only inventory and never changes source. Phase 6 guidance
is contextual/heuristic advice and cannot block a `0.1.2` release verdict.
