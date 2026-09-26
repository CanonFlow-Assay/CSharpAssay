# CSharpAssay Tool

`CsAssay.Tool` 0.2.0-rc.1 requires .NET 11 and analyzes only .NET 11 projects using C# preview. The default and only profile is `native`.

```sh
cs-assay doctor
cs-assay catalog
cs-assay check MySolution.slnx
cs-assay verify MySolution.slnx --json assay.json --sarif assay.sarif
```

`check` is provisional; `verify` requires complete evidence and required tests. Missing evidence never yields a passing verdict. The legacy `migrate` command and library adapters are removed. See the repository's .NET 11 migration report for validation and limitations.
