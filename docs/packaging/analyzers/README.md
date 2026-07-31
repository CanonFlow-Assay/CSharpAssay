# CSharpAssay analyzers

`CsAssay.Analyzers` supplies the admitted CSharpAssay Roslyn diagnostics to
editor and build hosts. The package embeds the analyzer, its three
CSharpAssay-owned runtime dependencies under `analyzers/dotnet/cs`, and a
transitive MSBuild enforcement target.

```xml
<PackageReference Include="CsAssay.Analyzers"
                  Version="0.1.0"
                  PrivateAssets="all" />
```

The package is a research preview. Only rules marked `Admitted` can block a
CSharpAssay release verdict. See the repository documentation for policy,
suppression, staged adoption, installation, and rollback guidance. The six
Phase 6 contextual/heuristic rules are informational and cannot block in
`0.1.0`.

`CsAssayEnforceOnBuild` defaults to `true`. Admitted violations therefore fail
ordinary `dotnet build`; disabling analyzers or listing an admitted CSharpAssay
rule in `NoWarn` fails with an explicit gate error. Set the property to `false`
only for a reviewed emergency rollback. The authoritative CLI remains required
for graph, all-target-framework, configured-test, and completeness evidence.
