# Installation: .NET 11 RC1

Install SDK `11.0.100-rc.1.26425.128`, target `net11.0`, and set `LangVersion` to `preview`. Earlier targets and compiler hosts are unsupported.

The development packages are `CsAssay.Analyzers` and `CsAssay.Tool` `0.2.0-rc.1`. Build locally using the repository instructions; publication is a separate release operation. The sibling playground's `eng/prepare-assay.sh` builds a local feed and installs its pinned local tool.

```sh
dotnet tool install CsAssay.Tool --version 0.2.0-rc.1 --add-source /path/to/local/feed
cs-assay doctor
cs-assay check MySolution.slnx
cs-assay verify MySolution.slnx --json assay.json --sarif assay.sarif
```

The only profile is `native`. The OneOf/ValueOf migration command and all legacy library-adapter support are removed.

`CsAssayEnforceOnBuild=false` remains an explicit reviewed rollback of Assay enforcement. It does not permit an older framework or language baseline. Compiler and unrelated warnings retain their existing treatment.
