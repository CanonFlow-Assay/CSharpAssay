using System.Collections.Immutable;
using CsAssay.Domain;

namespace CsAssay.Catalogue;

public static class RuleCatalogue
{
    private const string DocsRoot = "docs/rules/";
    private const string CompatPositive = "specimens/Compat.Good/";
    private const string CompatNegative = "specimens/Compat.Bad/";

    public static ImmutableArray<RuleRecord> All { get; } =
    [
        Rule(
            RuleIds.UnauthorizedSuppression,
            "CSharpAssay suppression must be authorized",
            RuleCategory.Policy,
            RuleCertainty.Deterministic,
            RuleDisposition.Block,
            "syntax, suppressed diagnostics, and reviewed policy",
            "pragma or SuppressMessage targeting a CSharpAssay diagnostic",
            "Only fingerprinted grants with owner, reason, and expiry are accepted."),
        Rule(
            RuleIds.NullableDisabled,
            "Nullable analysis must remain enabled",
            RuleCategory.Nullability,
            RuleCertainty.Deterministic,
            RuleDisposition.Block,
            "parse options, compilation options, and nullable directives",
            "#nullable disable directive or disabled project nullable context",
            "Boundary-scoped policy may exclude non-core code; exclusions remain evidence."),
        Rule(
            RuleIds.NullForgiving,
            "Null-forgiving operators must not erase compiler evidence",
            RuleCategory.Nullability,
            RuleCertainty.Deterministic,
            RuleDisposition.Block,
            "suppress-nullable-warning syntax and configured core boundary",
            "postfix null-forgiving operator",
            "Boundary conversion code must prove or explicitly model the value; ! is not an assertion."),
        Rule(
            RuleIds.NullValueIntroduction,
            "Core code must not introduce a null value",
            RuleCategory.Nullability,
            RuleCertainty.Deterministic,
            RuleDisposition.Block,
            "null/default operations and configured core boundary",
            "null literal or reference-typed default value used as data",
            "Null pattern checks are allowed only to convert boundary input into an explicit domain type."),
        Rule(
            RuleIds.NullableCoreContract,
            "Core public contracts must not expose nullable values",
            RuleCategory.Nullability,
            RuleCertainty.Deterministic,
            RuleDisposition.Block,
            "public symbol nullability and configured core boundary",
            "nullable return, parameter, property, field, event, or nested type argument",
            "Interop contracts belong in the shell and must convert once at the boundary."),
        Rule(
            RuleIds.MutableSetter,
            "Immutable data must not expose a mutable setter",
            RuleCategory.Immutability,
            RuleCertainty.Deterministic,
            RuleDisposition.Block,
            "property and containing-type symbols",
            "public set accessor on a public record",
            "A reviewed framework boundary may be excluded by policy."),
        Rule(
            RuleIds.MutableCollectionExposure,
            "Immutable carriers must not expose known mutable collections",
            RuleCategory.Immutability,
            RuleCertainty.Deterministic,
            RuleDisposition.Block,
            "property type metadata identity",
            "public record property exposing a known mutable collection interface or type",
            "A reviewed framework boundary may be excluded by policy."),
        Rule(
            RuleIds.SwallowedException,
            "Exceptions must not be silently swallowed",
            RuleCategory.Effects,
            RuleCertainty.Deterministic,
            RuleDisposition.Block,
            "catch-clause syntax and operation shape",
            "empty catch block",
            "Intentional handling must have an observable operation or an authorized suppression."),
        Rule(
            RuleIds.AsyncVoid,
            "Async methods must return an awaitable",
            RuleCategory.Async,
            RuleCertainty.Deterministic,
            RuleDisposition.Block,
            "method symbols and event-handler signature",
            "async void outside a recognized event handler",
            "Recognized event handlers are exempt."),
        Rule(
            RuleIds.BlockingAsync,
            "Async flow must not block on an awaitable",
            RuleCategory.Async,
            RuleCertainty.Deterministic,
            RuleDisposition.Block,
            "operation symbols inside an async method",
            "Task.Result, ValueTask.Result, or Wait() inside an async method",
            "No automatic suppression is granted for alleged hot paths."),
        Rule(
            RuleIds.ExtensibleClosedHierarchy,
            "Configured closed hierarchies must reject external derivation",
            RuleCategory.Union,
            RuleCertainty.Deterministic,
            RuleDisposition.Block,
            "configured metadata identity and constructor symbols",
            "externally accessible constructor on a configured closed record base",
            "The type must be declared in csassay_closed_types or .csassay.json."),
        Rule(
            RuleIds.IncompleteClosedHierarchySwitch,
            "Configured closed hierarchy switches must handle every known case",
            RuleCategory.Union,
            RuleCertainty.Deterministic,
            RuleDisposition.Block,
            "semantic switch model and direct-derived-type symbols",
            "switch over a configured closed hierarchy with an omitted direct case",
            "Only the restricted direct-case model is evaluated."),
        Rule(
            RuleIds.UnguardedOneOfExtraction,
            "OneOf ordinal extraction must be dominated by its matching guard",
            RuleCategory.Union,
            RuleCertainty.Deterministic,
            RuleDisposition.Block,
            "property metadata identity and operation ancestry",
            "AsTn access outside the proven guard subset",
            "Use Match/Switch or a semantically matched IsTn guard."),
        Rule(
            RuleIds.NativeUnionDiscard,
            "Native union switches should not hide new cases behind a discard",
            RuleCategory.Union,
            RuleCertainty.Deterministic,
            RuleDisposition.Block,
            "native union symbol identity and semantic switch model",
            "discard/default arm on a native union switch",
            "Native-preview only; unavailable toolchains produce missing evidence.",
            [EffectiveProfile.NativePreview])
    ];

    public static Presence<RuleRecord> Find(string id)
    {
        foreach (var rule in All)
        {
            if (string.Equals(rule.Id, id, StringComparison.OrdinalIgnoreCase))
            {
                return Presence.Of(rule);
            }
        }

        return Presence.Missing<RuleRecord>();
    }

    private static RuleRecord Rule(
        string id,
        string title,
        RuleCategory category,
        RuleCertainty certainty,
        RuleDisposition disposition,
        string evidence,
        string mechanism,
        string suppression) =>
        Rule(
            id,
            title,
            category,
            certainty,
            disposition,
            evidence,
            mechanism,
            suppression,
            [EffectiveProfile.Compat, EffectiveProfile.NativePreview]);

    private static RuleRecord Rule(
        string id,
        string title,
        RuleCategory category,
        RuleCertainty certainty,
        RuleDisposition disposition,
        string evidence,
        string mechanism,
        string suppression,
        ImmutableArray<EffectiveProfile> profiles) =>
        new(
            Id: id,
            Title: title,
            Category: category,
            Status: RuleStatus.Prototype,
            Certainty: certainty,
            Disposition: disposition,
            Profiles: profiles,
            RequiredEvidence: evidence,
            Mechanism: mechanism,
            SuppressionPolicy: suppression,
            PositiveSpecimen: CompatPositive + id,
            NegativeSpecimen: CompatNegative + id,
            Documentation: DocsRoot + id + ".md",
            DelegatedTo: Presence.Missing<string>());
}
