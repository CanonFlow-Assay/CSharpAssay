using System.Collections.Immutable;

namespace CsAssay.Workspaces;

public static class MigrationGuidance
{
    public static ImmutableArray<MigrationRisk> Risks(
        string representation,
        ImmutableArray<string> evidence) =>
        representation == "OneOf"
            ?
            [
                new MigrationRisk(
                    "MIG-ONEOF-PUBLIC-API",
                    "Changing a public return, parameter, member, base, or constraint type is a source and binary compatibility risk.",
                    evidence),
                new MigrationRisk(
                    "MIG-ONEOF-ORDINALS",
                    "Alternative order may be observable through Tn, AsTn, IsTn, TryPickTn, or serialized discriminators.",
                    evidence),
                new MigrationRisk(
                    "MIG-ONEOF-REPRESENTATION",
                    "Native union layout, boxing, equality, default values, reflection, and wire behavior require measurement or round-trip evidence.",
                    evidence)
            ]
            :
            [
                new MigrationRisk(
                    "MIG-VALUEOF-PUBLIC-API",
                    "Changing a public ValueOf-derived class to another wrapper is a source and binary compatibility risk.",
                    evidence),
                new MigrationRisk(
                    "MIG-VALUEOF-SEMANTICS",
                    "Class-to-struct migration changes identity, default values, equality, generic constraints, boxing, and conversion behavior.",
                    evidence),
                new MigrationRisk(
                    "MIG-VALUEOF-VALIDATION",
                    "ValueOf validation is exception-based unless the application proves and exposes a total factory.",
                    evidence)
            ];

    public static MigrationBehaviorComparison Comparison(string representation) =>
        representation == "OneOf"
            ? new MigrationBehaviorComparison(
                "OneOf/OneOfBase compatibility representation",
                "native union or project-owned stable facade",
                "manual reviewed migration; no representation swap is implied",
                [
                    "case identity and ordering",
                    "Match/Switch result and side-effect ordering",
                    "equality, hash, default, and reflection behavior",
                    "exception and cancellation behavior",
                    "JSON and HTTP wire contracts",
                    "allocation, boxing, and layout where performance matters"
                ])
            : new MigrationBehaviorComparison(
                "ValueOf-derived reference type",
                "native record/class wrapper or qualified generator",
                "manual reviewed migration; choose class or struct from domain and integration evidence",
                [
                    "validation success and failure outcomes",
                    "equality, hash, conversion, and default behavior",
                    "generic constraints and binary signatures",
                    "JSON and HTTP wire contracts",
                    "EF key/converter/change-tracking behavior",
                    "allocation and boxing where performance matters"
                ]);

    public static ImmutableArray<MigrationAdapterAssessment> AdapterAssessments(
        string representation) =>
        [
            new MigrationAdapterAssessment(
                "System.Text.Json",
                "required-unqualified",
                "public or persisted representation may be a wire contract",
                [
                    "round-trip every case/value including invalid and default states",
                    "compare property names, discriminators, payload shape, and failure behavior",
                    "preserve a golden payload for each supported version"
                ]),
            new MigrationAdapterAssessment(
                "EF Core",
                representation == "ValueOf"
                    ? "required-unqualified"
                    : "context-required",
                representation == "ValueOf"
                    ? "value object may be a key, property, converter, or comparer"
                    : "sum type persistence is application-specific and cannot be inferred from a signature",
                [
                    "prove conversion, comparison, key generation, query translation, and change tracking",
                    "prove invalid/default database values cannot silently enter the core"
                ]),
            new MigrationAdapterAssessment(
                "ASP.NET Core/OpenAPI",
                "context-required",
                "public API exposure may cross an endpoint boundary",
                [
                    "prove model binding and response mapping for success and every failure case",
                    "compare status codes, content types, payloads, and generated OpenAPI"
                ]),
            new MigrationAdapterAssessment(
                "NativeAOT",
                "context-required",
                "reflection, converters, or generated metadata may be trimmed",
                [
                    "publish and execute a representative trimmed NativeAOT application",
                    "treat trim/AOT warnings, missing metadata, and reflection fallback as failed evidence"
                ])
        ];

    public static ImmutableArray<MigrationRecommendation> Recommendations(
        string representation,
        string api,
        ImmutableArray<string> evidence) =>
        [
            new MigrationRecommendation(
                "MIG-BASELINE-" + representation.ToUpperInvariant(),
                api,
                "Capture source, binary, behavioral, and wire baselines for this exact API before selecting a target representation.",
                evidence,
                [
                    "public API compatibility diff",
                    "golden behavior comparison",
                    "applicable adapter assessments"
                ]),
            new MigrationRecommendation(
                "MIG-DECIDE-" + representation.ToUpperInvariant(),
                api,
                "Either retain the compatibility representation behind a stable facade or approve a versioned breaking change with evidence; do not use find-and-replace.",
                evidence,
                [
                    "reviewed migration decision",
                    "rollback plan",
                    "consumer and integration qualification"
                ])
        ];

    public static ImmutableArray<MigrationEcosystemAdapter> EcosystemAdapters(
        ImmutableArray<MigrationExposure> exposures)
    {
        var oneOfObserved = exposures.Any(exposure =>
            exposure.Representation == "OneOf");
        var valueOfObserved = exposures.Any(exposure =>
            exposure.Representation == "ValueOf");
        return
        [
            new MigrationEcosystemAdapter(
                "OneOf",
                oneOfObserved ? "observation-only" : "not-observed",
                "unqualified",
                "Metadata identity can be inventoried; no package version is promoted as a behavioral evidence provider."),
            new MigrationEcosystemAdapter(
                "ValueOf",
                valueOfObserved ? "legacy-observation-only" : "not-observed",
                "unqualified",
                "Metadata identity and inheritance can be inventoried; exception, serializer, and ORM behavior remain unqualified."),
            new MigrationEcosystemAdapter(
                "Vogen",
                "not-enabled",
                "unqualified",
                "No exact package version has passed the package and framework adapter corpus."),
            new MigrationEcosystemAdapter(
                "dunet",
                "not-enabled",
                "unqualified",
                "No exact package version has passed generated-symbol, behavior, serializer, and AOT qualification."),
            new MigrationEcosystemAdapter(
                "Thinktecture.Runtime.Extensions",
                "not-enabled",
                "unqualified",
                "No exact package version has passed generated-symbol, behavior, serializer, EF, ASP.NET, and AOT qualification.")
        ];
    }
}
