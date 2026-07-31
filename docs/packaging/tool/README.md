# CSharpAssay tool

`CsAssay.Tool` installs the `cs-assay` command-line verifier.

```text
dotnet tool install --global CsAssay.Tool --version 0.1.0 \
  --add-source ./packages
cs-assay doctor
cs-assay check MySolution.slnx
cs-assay verify MySolution.slnx
```

`check` is provisional. `verify` is authoritative and returns exit code 0
only when the configured compiler, analyzer, target-framework, rule, and test
evidence is complete and clean. See the repository documentation for full
installation, CI, evidence, and rollback guidance. Registry publication is not
yet authorized; install from package bytes and provenance produced by the same
trusted workflow run or from an explicitly configured internal feed.
