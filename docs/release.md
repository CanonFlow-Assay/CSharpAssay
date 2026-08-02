# Release qualification

Phase 4 produces two release packages:

- `CsAssay.Analyzers.0.1.2.nupkg`;
- `CsAssay.Tool.0.1.2.nupkg`.

Run the stable release sequence from the repository root:

```text
dotnet restore CSharpAssay.slnx --locked-mode
dotnet build CSharpAssay.slnx --no-restore --configuration Release
dotnet test CSharpAssay.slnx --no-build --no-restore \
  --configuration Release --max-parallel-test-modules 1
./eng/pack-release.sh
./eng/qualify-packages.sh
```

Release packaging requires a clean working tree so the commit recorded in the
package metadata and provenance is the exact source of every payload byte.
The script first re-establishes the repository's ordinary stable Release build,
so stale output from a prior `-p:LangVersion=preview` experiment cannot enter a
stable package.

`pack-release.sh` performs two independent packs. The repository-owned audit
normalizes unsigned NuGet OPC/ZIP metadata before signing, validates required
payload and the embedded commit/identity/license metadata, rejects source,
`.git`, environment-file, and inspiration repository entries, and compares
both artifact sets byte for byte. It writes:

```text
artifacts/packages/CsAssay.Analyzers.0.1.2.nupkg
artifacts/packages/CsAssay.Tool.0.1.2.nupkg
artifacts/packages/checksums.sha256
artifacts/packages/provenance.json
```

`qualify-packages.sh` installs the tool from a local-only NuGet source, runs
`doctor`, performs authoritative sample verification, builds a clean analyzer
consumer with repository-wide warning promotion disabled, and proves the
packaged target itself promotes an ordinary `CSAN0004` warning into a blocking
diagnostic. It also rejects `RunAnalyzers=false`, admitted `NoWarn`, and admitted
`WarningsNotAsErrors`. Its reviewed-rollback fixture enables global
warnings-as-errors, proves admitted CSharpAssay diagnostics remain visible but
nonblocking, and proves an unrelated compiler warning still blocks.

## Signing order

No NuGet package-signing certificate is configured. Current packages are
therefore deliberately unsigned, while main-branch and publication CI attach
signed keyless build provenance through GitHub/Sigstore. Registry authentication
uses NuGet trusted publishing with a one-hour temporary key, not a stored
long-lived API key.

If the project later supplies an authorized NuGet certificate, the required
order is:

```text
pack -> normalize -> audit unsigned payload -> sign -> audit without rewriting
```

Never normalize or otherwise rewrite a signed package. The manual publication
workflow reruns the entire gate, attests the tested bytes, exchanges GitHub OIDC
for a temporary NuGet credential, and pushes the two explicit package paths.
See [publishing.md](publishing.md).

## CI authority separation

The stable .NET 10 job is the only authoritative release gate. It uses locked
restore, serialized tests, authoritative self-verification, package
qualification, SARIF upload, checksums, and provenance attestation.

The native-preview job is separate and `continue-on-error`. It records preview
capability evidence but cannot block or promote the stable release lane. A
preview failure becomes authoritative only after policy and qualification are
explicitly changed in a reviewed commit.
