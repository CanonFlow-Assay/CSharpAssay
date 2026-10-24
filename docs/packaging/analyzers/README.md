# CSharpAssay analyzers

`CsAssay.Analyzers` supplies the admitted CSharpAssay Roslyn diagnostics to
editor and build hosts running .NET 11. Consumer projects must target
`net11.0` and use `LangVersion=preview`; older targets and hosts are unsupported. The package embeds the analyzer, its three
CSharpAssay-owned runtime dependencies under `analyzers/dotnet/cs`, and a
transitive MSBuild enforcement target.

```xml
<PackageReference Include="CsAssay.Analyzers"
                  Version="0.2.0-rc.1"
                  PrivateAssets="all" />
```

The package is a research preview. Only rules marked `Admitted` can block a
CSharpAssay release verdict. See the repository documentation for policy,
suppression, staged adoption, installation, and rollback guidance. The six
Phase 6 contextual/heuristic rules are informational and cannot block in
`0.2.0-rc.1`.

`CsAssayEnforceOnBuild` defaults to `true`. Admitted violations therefore fail
ordinary `dotnet build`; disabling analyzers or listing an admitted CSharpAssay
rule in `NoWarn` fails with an explicit gate error. Set the property to `false`
only for a reviewed emergency rollback. Under global warnings-as-errors, that
rollback keeps CSharpAssay diagnostics visible while exempting only admitted
CSharpAssay IDs from error promotion; unrelated warnings remain errors.

The analyzer and verifier now share normalized Core/Shell and fingerprinted-grant
policy. The package supplies the nearest ancestor .csassay.json as an AdditionalFile,
or accepts an explicit CsAssayPolicyPath. Selected shell findings remain visible
warnings even under global warnings-as-errors; core blockers are
errors. Malformed policy and suppression/nullable-flow integrity failures block.
Accepted grants remain observations and do not authenticate human approval.

The authoritative CLI remains required for evaluated graph, all target frameworks,
configured tests and completeness. Supported compiler/editor policy inputs do
not establish qualification of every IDE host. IDE analysis can be disabled
independently; removing enforcement or controlling both policy and code is outside
this bounded guarantee. See docs/agent-overhaul/boundaries-and-output.md for
matching defaults, deliberate identity migration, editorconfig guards and limits.
