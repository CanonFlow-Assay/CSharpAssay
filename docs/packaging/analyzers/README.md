# CSharpAssay analyzers

`CsAssay.Analyzers` supplies the admitted CSharpAssay Roslyn diagnostics to
editor and build hosts. The package embeds only the analyzer and its three
CSharpAssay-owned runtime dependencies under `analyzers/dotnet/cs`.

```xml
<PackageReference Include="CsAssay.Analyzers"
                  Version="0.1.0"
                  PrivateAssets="all" />
```

The package is a research preview. Only rules marked `Admitted` can block a
CSharpAssay release verdict. See the repository documentation for policy,
suppression, installation, and rollback guidance. Registry publication is not
yet authorized; use package bytes and provenance from the same trusted workflow
run or an explicitly configured internal feed.
