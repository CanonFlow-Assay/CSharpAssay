# Publishing `0.1.2` to NuGet.org

Publication is manual, passwordless, and gated by the same source commit that
produces the package bytes. Do not create or store a long-lived NuGet API key.
The workflow follows Microsoft’s
[NuGet trusted publishing guidance](https://learn.microsoft.com/nuget/nuget-org/trusted-publishing).

## One-time NuGet.org setup

1. Sign in to NuGet.org as [`arun6202`](https://www.nuget.org/profiles/arun6202),
   the intended owner of both package IDs.
2. Open the username menu, choose **Trusted Publishing**, and add a GitHub
   policy with:

   | Field | Value |
   | --- | --- |
   | Repository owner | `CanonFlow-Assay` |
   | Repository | `CSharpAssay` |
   | Workflow file | `publish-nuget.yml` |
   | Environment | `nuget.org` |

   Enter only the workflow filename, not `.github/workflows/`.
3. In GitHub repository settings, create the `nuget.org` environment. Protect
   it with a required reviewer if available.

The public NuGet profile name `arun6202` is declared directly in the workflow.
No NuGet API key or GitHub secret is required; `NuGet/login@v1` exchanges the
job's GitHub OIDC identity for a short-lived key after qualification succeeds.

NuGet may show a temporarily active policy while it resolves immutable GitHub
repository/owner IDs. Run the first publication during that window; a successful
OIDC exchange permanently binds the policy.

## Publish

1. Confirm the reviewed release candidate is merged and all required checks are
   green.
2. Create the immutable `v0.1.2` tag at the exact reviewed candidate commit,
   not at a later merge or documentation commit.
3. Open **Actions → Publish NuGet packages → Run workflow**.
4. Select `v0.1.2` and enter `publish-0.1.2` exactly.
5. Approve the `nuget.org` environment deployment if protection is enabled.

The workflow performs locked restore, warning-clean build, all serialized
tests, authoritative self-assay, two reproducible packs, isolated fresh-install
qualification, package attestation, OIDC login, and two explicit pushes. The
temporary NuGet key is requested only after qualification. The tag makes the
reviewed candidate SHA the package provenance commit even when repository
policy retains a separate PR merge commit.

## Verify publication

NuGet indexing can take several minutes. Confirm both package pages, then use a
fresh cache:

```text
dotnet nuget locals all --clear
dotnet tool install --global CsAssay.Tool --version 0.1.2
cs-assay doctor

dotnet new classlib -n AssayConsumer
cd AssayConsumer
dotnet add package CsAssay.Analyzers --version 0.1.2
dotnet build
```

Compare downloaded package hashes with the workflow’s `checksums.sha256` and
verify its GitHub attestation. NuGet versions are immutable: any correction
after publication requires a new version, never replacement bytes.
