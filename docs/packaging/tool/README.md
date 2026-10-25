# CSharpAssay Tool

`CsAssay.Tool` 0.2.0-rc.1 requires .NET 11 and analyzes only .NET 11 projects using C# preview. The default and only profile is `native`.

```sh
cs-assay doctor
cs-assay doctor MySolution.slnx --policy .csassay.json
cs-assay catalog
cs-assay explain CSAI0001
cs-assay check MySolution.slnx
cs-assay verify MySolution.slnx --json assay.json --sarif assay.sarif --html assay.html --markdown assay.md
```

`check` is provisional; `verify` requires complete evidence and required tests. Missing evidence never yields a passing verdict. The legacy `migrate` command and library adapters are removed. See the repository's .NET 11 migration report for validation and limitations.

`explain` includes offline bundled canonical guides and before/after examples.
Doctor establishes prerequisites only: SDK context/pin, process identities,
native-symbol capability and policy parse/scope declarations. It runs no restore,
tests or compilation and cannot grant release authority. HTML/Markdown summaries
show the recorded outcome and effective findings, including suppressions and
missing evidence. Markdown is optional on check/verify/assess; output failure is
ToolFailure, not a successful verification. The development package must be
built/installed from the documented local feed; no public NuGet availability is
implied. See repository docs/adoption.md and docs/expenseguard-first-change.md.
