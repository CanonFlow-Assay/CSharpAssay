# Supported .NET 11 SDK prerequisite

## Read-only prerequisite diagnostics

The developer-experience increment adds optional context to the existing command:

```text
cs-assay doctor [project-or-solution] [--policy path]
```

The no-input form uses the current directory. An input selects the directory
used for `dotnet --version` and ancestor `global.json` discovery; the running
runtime is reported separately and is never treated as SDK identity. The exact
supported SDK comes from the tool's bundled repository pin. MSBuild output names
the instance registered for this tool process, with its locator version/path;
it is not a claim that all projects selected that instance or target framework.
Roslyn assembly identity and native-union symbol APIs are separate observations.

Policy discovery/parsing is shared with verification. The output lists declared
core/shell selectors, but does not load a graph or establish selector matches.
No policy is required for a host-only/no-input observation; a supplied application
context needs its release policy. A missing pin/SDK/policy or unsupported fact
returns 2. Unexpected execution/cancellation returns 3; unsupported command options
remain usage exit64. Exit0 means the observed prerequisites checked successfully.

Doctor runs no restore, SDK install, tests, compilation probes or network scan,
and writes no reports/source. Missing restoration/build-dependent facts stay
unestablished. The final line explicitly says application verification has not
run and points to verify. Doctor is never release authority.

The historical prerequisite qualification record follows.

This is a toolchain-only prerequisite to the boundary/output overhaul. No rule
admission, policy classification, suppression authorization, console format,
machine protocol or performance budget changes are included in this step.

The previous SDK `11.0.100-rc.1.26425.128` is retained for historical baselines.
Its support window ended on 2026-10-13. The selected SDK
`11.0.100-rc.2.26504.105` is the current .NET 11 go-live release, supported until
2026-11-10 according to the [Microsoft support policy](https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core).
Future releases need a new explicit pin and qualification.

The official Linux-x64 archive was downloaded from the
[release metadata](https://builds.dotnet.microsoft.com/dotnet/release-metadata/11.0/releases.json)
and SHA-512 checked before extraction. See the baseline contract for its exact
hash. The isolated installation is `/home/arun/.cache/csharp-overhaul-sdk/dotnet-rc2`;
the earlier SDK installation was not replaced. Both repositories retain
`net11.0`, preview language, nullable and warnings-as-errors requirements, and
the Playground's reviewed negative/upstream fixture exceptions remain unchanged.

## Independent native-union probes

The SDK's compiler reports `5.12.0-1.26504.105`. The runner's pinned Roslyn package
surface remains 5.9.0; full release/package qualification must establish their
bounded interoperability. Runtime release support does not confer support or
production qualification on a preview language feature.

- Preview compilation and execution of `union Outcome(Accepted, Rejected)`,
  conversion, matching and boxed `IUnion` identity passed, returning 42.
- Explicit C# 14 compilation rejected the union syntax with compiler errors.
- A preview switch omitting `Rejected` was rejected with CS8509 under the same
  warnings-as-errors setting. One earlier invocation exited without diagnostics
  and is retained as unqualified, not relabeled a valid negative control. The
  fresh no-build-server invocation produced the actual CS8509 evidence.

Probe sources and raw output are under `/home/arun/.cache/csharp-overhaul-sdk/`.
The [C# union reference](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/builtin-types/union)
describes the feature; these actual probes, rather than runtime version alone,
establish the limited capability exercised here. No union library, polyfill,
source generator, older-framework lane or .NET 12 implementation is introduced.

## Reproduce prerequisite qualification

Use the isolated SDK at the front of PATH and set DOTNET_ROOT to that directory
for its own host/child processes. Disable MSBuild server reuse while switching
between isolated RC1 and RC2 runs. From clean committed engine source:

```sh
dotnet build CSharpAssay.slnx -c Release
dotnet eng/CsAssay.Qualification/bin/Release/net11.0/CsAssay.Qualification.dll \
  qualify --output artifacts/agent-overhaul/sdk-prerequisite
```

Prepare the paired Playground from that exact qualified engine revision, commit
only deliberate lock refreshes, and run its full existing paired qualification.
Actual source commits, logs, hashes and results are recorded separately. Until
those gates complete this document is an implementation plan, not a qualification
claim. Policy/output implementation starts after the prerequisite passes.
