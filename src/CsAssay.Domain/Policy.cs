using System.Collections.Immutable;

namespace CsAssay.Domain;

public sealed record ReleasePolicy(
    bool AllowPreviewToolchain,
    ImmutableArray<string> RequiredTargetFrameworks);

public sealed record BoundaryPolicy(
    ImmutableArray<string> CoreNamespaces,
    ImmutableArray<string> ShellNamespaces);

public sealed record RepresentationPolicy(
    ImmutableArray<string> ResultTypes,
    ImmutableArray<string> OptionTypes,
    ImmutableArray<string> ClosedTypes);

public sealed record SuppressionGrant(
    string RuleId,
    string Owner,
    string Reason,
    DateTimeOffset Expires,
    string Fingerprint);

public sealed record AssayPolicy(
    AssayProfile Profile,
    ReleasePolicy Release,
    BoundaryPolicy Boundaries,
    RepresentationPolicy Representations,
    ImmutableDictionary<string, ImmutableArray<string>> DomainPrimitives,
    ImmutableArray<SuppressionGrant> Suppressions)
{
    public static AssayPolicy Observe { get; } = new(
        AssayProfile.Auto,
        new ReleasePolicy(
            AllowPreviewToolchain: false,
            RequiredTargetFrameworks: ImmutableArray<string>.Empty),
        new BoundaryPolicy(
            CoreNamespaces: ImmutableArray<string>.Empty,
            ShellNamespaces: ImmutableArray<string>.Empty),
        new RepresentationPolicy(
            ResultTypes: ImmutableArray<string>.Empty,
            OptionTypes: ImmutableArray<string>.Empty,
            ClosedTypes: ImmutableArray<string>.Empty),
        ImmutableDictionary<string, ImmutableArray<string>>.Empty,
        ImmutableArray<SuppressionGrant>.Empty);
}
