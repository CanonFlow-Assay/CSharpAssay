using System.Collections.Immutable;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using CsAssay.Domain;

namespace CsAssay.Workspaces;

public sealed record PolicyLoadResult(
    AssayPolicy Policy,
    Presence<string> Path,
    ImmutableArray<EvaluationFailure> Failures,
    bool UsedDefault);

public static class PolicyLoader
{
    private static readonly Regex MetadataNameRegex = new(
        "^[A-Za-z_][A-Za-z0-9_]*(?:\\.[A-Za-z_][A-Za-z0-9_]*|\\+[A-Za-z_][A-Za-z0-9_]*)*(?:`[1-9][0-9]*)?$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex TargetFrameworkRegex = new(
        "^net[0-9]+(?:\\.[0-9]+)?(?:-[A-Za-z0-9.-]+)?$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex RuleIdRegex = new(
        "^CSA[NUIEFDAP][0-9]{4}$",
        RegexOptions.CultureInvariant |
        RegexOptions.Compiled);
    private static readonly Regex Sha256Regex = new(
        "^[0-9a-f]{64}$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex ConfigurationRegex = new(
        "^[A-Za-z0-9_.-]+$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly ImmutableHashSet<string> RootKeys =
        ImmutableHashSet.Create(
            StringComparer.Ordinal,
            "$schema",
            "profile",
            "release",
            "boundaries",
            "representations",
            "domainPrimitives",
            "suppressions");

    public static PolicyLoadResult Load(
        string inputPath,
        Presence<string> explicitPolicyPath,
        bool requirePolicy)
    {
        try
        {
            var policyPath = explicitPolicyPath switch
            {
                Presence<string>.Present present =>
                    Presence.Of(Path.GetFullPath(present.Value)),
                _ => FindPolicy(inputPath)
            };

            if (policyPath is not Presence<string>.Present found)
            {
                var failures = requirePolicy
                    ? ImmutableArray.Create(new EvaluationFailure(
                        "CSASSAY-CONFIG-MISSING",
                        "Authoritative verification requires .csassay.json.",
                        "policy",
                        Presence.Missing<string>()))
                    : ImmutableArray<EvaluationFailure>.Empty;
                return new PolicyLoadResult(
                    AssayPolicy.Observe,
                    Presence.Missing<string>(),
                    failures,
                    UsedDefault: true);
            }

            if (!File.Exists(found.Value))
            {
                return Failure(
                    "CSASSAY-CONFIG-NOT-FOUND",
                    "Policy file does not exist: " + found.Value);
            }

            var policy = Parse(File.ReadAllText(found.Value));
            return new PolicyLoadResult(
                policy,
                policyPath,
                ImmutableArray<EvaluationFailure>.Empty,
                UsedDefault: false);
        }
        catch (Exception exception) when (
            exception is JsonException or
            InvalidDataException or
            IOException or
            UnauthorizedAccessException)
        {
            return Failure("CSASSAY-CONFIG-INVALID", exception.Message);
        }
    }

    public static AssayPolicy Parse(string json)
    {
        using var document = JsonDocument.Parse(
            json,
            new JsonDocumentOptions
            {
                AllowTrailingCommas = false,
                CommentHandling = JsonCommentHandling.Disallow
            });

        var root = RequireObject(document.RootElement, "$");
        EnsureUniqueAndAllowed(root, RootKeys, "$");

        var profile = root.TryGetProperty("profile", out var profileElement)
            ? ParseProfile(RequireString(profileElement, "$.profile"))
            : AssayProfile.Auto;

        var release = root.TryGetProperty("release", out var releaseElement)
            ? ParseRelease(releaseElement)
            : new ReleasePolicy(
                false,
                ImmutableArray<string>.Empty,
                ImmutableArray<string>.Empty,
                ImmutableArray<TestRequirement>.Empty);
        var boundaries = root.TryGetProperty("boundaries", out var boundariesElement)
            ? ParseBoundaries(boundariesElement)
            : new BoundaryPolicy(
                ImmutableArray<string>.Empty,
                ImmutableArray<string>.Empty,
                ImmutableArray<string>.Empty,
                ImmutableArray<string>.Empty);
        var representations = root.TryGetProperty(
            "representations",
            out var representationsElement)
            ? ParseRepresentations(representationsElement)
            : new RepresentationPolicy(
                ImmutableArray<string>.Empty,
                ImmutableArray<string>.Empty,
                ImmutableArray<string>.Empty);
        var domainPrimitives = root.TryGetProperty(
            "domainPrimitives",
            out var primitivesElement)
            ? ParseDomainPrimitives(primitivesElement)
            : ImmutableDictionary<string, ImmutableArray<string>>.Empty;
        var suppressions = root.TryGetProperty("suppressions", out var suppressionsElement)
            ? ParseSuppressions(suppressionsElement)
            : ImmutableArray<SuppressionGrant>.Empty;

        return new AssayPolicy(
            profile,
            release,
            boundaries,
            representations,
            domainPrimitives,
            suppressions);
    }

    private static ReleasePolicy ParseRelease(JsonElement element)
    {
        var value = RequireObject(element, "$.release");
        EnsureUniqueAndAllowed(
            value,
            [
                "allowPreviewToolchain",
                "requiredTargetFrameworks",
                "requiredRules",
                "tests"
            ],
            "$.release");

        var allowPreview = value.TryGetProperty(
            "allowPreviewToolchain",
            out var previewElement)
            ? RequireBoolean(previewElement, "$.release.allowPreviewToolchain")
            : false;
        var frameworks = value.TryGetProperty(
            "requiredTargetFrameworks",
            out var frameworksElement)
            ? ParseStringArray(
                frameworksElement,
                "$.release.requiredTargetFrameworks")
            : ImmutableArray<string>.Empty;

        foreach (var framework in frameworks)
        {
            if (!TargetFrameworkPattern().IsMatch(framework))
            {
                throw new InvalidDataException(
                    "Invalid target framework in $.release.requiredTargetFrameworks: " +
                    framework);
            }
        }

        var requiredRules = value.TryGetProperty(
            "requiredRules",
            out var requiredRulesElement)
            ? ParseStringArray(
                requiredRulesElement,
                "$.release.requiredRules")
            : ImmutableArray<string>.Empty;
        foreach (var ruleId in requiredRules)
        {
            if (!RuleIdPattern().IsMatch(ruleId))
            {
                throw new InvalidDataException(
                    "Invalid rule ID in $.release.requiredRules: " + ruleId);
            }
        }

        var tests = value.TryGetProperty("tests", out var testsElement)
            ? ParseTests(testsElement)
            : ImmutableArray<TestRequirement>.Empty;

        return new ReleasePolicy(
            allowPreview,
            frameworks,
            requiredRules,
            tests);
    }

    private static ImmutableArray<TestRequirement> ParseTests(
        JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidDataException("$.release.tests must be an array.");
        }

        var builder = ImmutableArray.CreateBuilder<TestRequirement>();
        var inputs = new HashSet<string>(StringComparer.Ordinal);
        var index = 0;
        foreach (var item in element.EnumerateArray())
        {
            var path = "$.release.tests[" +
                index.ToString(CultureInfo.InvariantCulture) + "]";
            var value = RequireObject(item, path);
            EnsureUniqueAndAllowed(
                value,
                [
                    "input",
                    "configuration",
                    "noBuild",
                    "minimumExpectedTests"
                ],
                path);

            var input = RequiredPropertyString(value, "input", path);
            var normalizedInput = input.Replace('\\', '/');
            if (Path.IsPathRooted(input) ||
                normalizedInput.Split('/').Contains(
                    "..",
                    StringComparer.Ordinal) ||
                Path.GetExtension(input) is not (
                    ".csproj" or ".sln" or ".slnx"))
            {
                throw new InvalidDataException(
                    path + ".input must be a repository-relative .csproj, .sln, or .slnx path.");
            }

            if (!inputs.Add(normalizedInput))
            {
                throw new InvalidDataException(
                    "$.release.tests cannot contain duplicate inputs.");
            }

            var configuration = value.TryGetProperty(
                "configuration",
                out var configurationElement)
                ? RequireString(
                    configurationElement,
                    path + ".configuration")
                : "Release";
            if (!ConfigurationRegex.IsMatch(configuration))
            {
                throw new InvalidDataException(
                    path + ".configuration is invalid.");
            }

            var noBuild = value.TryGetProperty(
                "noBuild",
                out var noBuildElement) &&
                RequireBoolean(noBuildElement, path + ".noBuild");
            var minimumExpectedTests = value.TryGetProperty(
                "minimumExpectedTests",
                out var minimumElement)
                ? RequirePositiveInt32(
                    minimumElement,
                    path + ".minimumExpectedTests")
                : 1;

            builder.Add(new TestRequirement(
                normalizedInput,
                configuration,
                noBuild,
                minimumExpectedTests));
            index++;
        }

        return builder.ToImmutable();
    }

    private static BoundaryPolicy ParseBoundaries(JsonElement element)
    {
        var value = RequireObject(element, "$.boundaries");
        EnsureUniqueAndAllowed(
            value,
            [
                "coreProjects",
                "shellProjects",
                "coreNamespaces",
                "shellNamespaces"
            ],
            "$.boundaries");
        return new BoundaryPolicy(
            value.TryGetProperty("coreProjects", out var coreProjects)
                ? ParseStringArray(
                    coreProjects,
                    "$.boundaries.coreProjects")
                : ImmutableArray<string>.Empty,
            value.TryGetProperty("shellProjects", out var shellProjects)
                ? ParseStringArray(
                    shellProjects,
                    "$.boundaries.shellProjects")
                : ImmutableArray<string>.Empty,
            value.TryGetProperty("coreNamespaces", out var core)
                ? ParseStringArray(core, "$.boundaries.coreNamespaces")
                : ImmutableArray<string>.Empty,
            value.TryGetProperty("shellNamespaces", out var shell)
                ? ParseStringArray(shell, "$.boundaries.shellNamespaces")
                : ImmutableArray<string>.Empty);
    }

    private static RepresentationPolicy ParseRepresentations(JsonElement element)
    {
        var value = RequireObject(element, "$.representations");
        EnsureUniqueAndAllowed(
            value,
            ["resultTypes", "optionTypes", "closedTypes"],
            "$.representations");

        var resultTypes = value.TryGetProperty("resultTypes", out var result)
            ? ParseStringArray(result, "$.representations.resultTypes")
            : ImmutableArray<string>.Empty;
        var optionTypes = value.TryGetProperty("optionTypes", out var option)
            ? ParseStringArray(option, "$.representations.optionTypes")
            : ImmutableArray<string>.Empty;
        var closedTypes = value.TryGetProperty("closedTypes", out var closed)
            ? ParseStringArray(closed, "$.representations.closedTypes")
            : ImmutableArray<string>.Empty;

        foreach (var metadataName in resultTypes.Concat(optionTypes).Concat(closedTypes))
        {
            if (!MetadataNamePattern().IsMatch(metadataName))
            {
                throw new InvalidDataException(
                    "Invalid metadata name in $.representations: " + metadataName);
            }
        }

        return new RepresentationPolicy(resultTypes, optionTypes, closedTypes);
    }

    private static ImmutableDictionary<string, ImmutableArray<string>>
        ParseDomainPrimitives(JsonElement element)
    {
        var value = RequireObject(element, "$.domainPrimitives");
        var builder = ImmutableDictionary.CreateBuilder<
            string,
            ImmutableArray<string>>(StringComparer.Ordinal);
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var property in value.EnumerateObject())
        {
            if (!seen.Add(property.Name))
            {
                throw new InvalidDataException(
                    "Duplicate property $.domainPrimitives." + property.Name);
            }

            if (!MetadataNamePattern().IsMatch(property.Name))
            {
                throw new InvalidDataException(
                    "Invalid metadata name in $.domainPrimitives: " + property.Name);
            }

            builder.Add(
                property.Name,
                ParseStringArray(
                    property.Value,
                    "$.domainPrimitives." + property.Name));
        }

        return builder.ToImmutable();
    }

    private static ImmutableArray<SuppressionGrant> ParseSuppressions(
        JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidDataException("$.suppressions must be an array.");
        }

        var builder = ImmutableArray.CreateBuilder<SuppressionGrant>();
        var index = 0;
        foreach (var item in element.EnumerateArray())
        {
            var path = "$.suppressions[" +
                index.ToString(CultureInfo.InvariantCulture) + "]";
            var value = RequireObject(item, path);
            EnsureUniqueAndAllowed(
                value,
                ["ruleId", "owner", "reason", "expires", "fingerprint"],
                path);

            var ruleId = RequiredPropertyString(value, "ruleId", path);
            var owner = RequiredPropertyString(value, "owner", path);
            var reason = RequiredPropertyString(value, "reason", path);
            var expiresText = RequiredPropertyString(value, "expires", path);
            var fingerprint = RequiredPropertyString(value, "fingerprint", path);

            if (!RuleIdPattern().IsMatch(ruleId))
            {
                throw new InvalidDataException(path + ".ruleId is invalid.");
            }

            if (!DateTimeOffset.TryParseExact(
                    expiresText,
                    "yyyy-MM-dd",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal,
                    out var expires))
            {
                throw new InvalidDataException(
                    path + ".expires must use yyyy-MM-dd.");
            }

            if (!Sha256Pattern().IsMatch(fingerprint))
            {
                throw new InvalidDataException(
                    path + ".fingerprint must be a lowercase SHA-256 value.");
            }

            builder.Add(new SuppressionGrant(
                ruleId,
                owner,
                reason,
                expires,
                fingerprint));
            index++;
        }

        return builder.ToImmutable();
    }

    private static AssayProfile ParseProfile(string profile) =>
        profile switch
        {
            "auto" => AssayProfile.Auto,
            "compat" => AssayProfile.Compat,
            "native" => AssayProfile.Native,
            _ => throw new InvalidDataException(
                "$.profile must be auto, compat, or native.")
        };

    private static ImmutableArray<string> ParseStringArray(
        JsonElement element,
        string path)
    {
        if (element.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidDataException(path + " must be an array.");
        }

        var values = element.EnumerateArray()
            .Select((value, index) => RequireString(
                value,
                path + "[" + index.ToString(CultureInfo.InvariantCulture) + "]"))
            .ToImmutableArray();

        if (values.Any(string.IsNullOrWhiteSpace))
        {
            throw new InvalidDataException(path + " cannot contain empty values.");
        }

        if (values.Distinct(StringComparer.Ordinal).Count() != values.Length)
        {
            throw new InvalidDataException(path + " cannot contain duplicates.");
        }

        return values;
    }

    private static JsonElement RequireObject(JsonElement element, string path)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException(path + " must be an object.");
        }

        return element;
    }

    private static string RequireString(JsonElement element, string path)
    {
        if (element.ValueKind != JsonValueKind.String)
        {
            throw new InvalidDataException(path + " must be a string.");
        }

        return element.GetString() switch
        {
            string value => value,
            _ => throw new InvalidDataException(path + " must be a string.")
        };
    }

    private static bool RequireBoolean(JsonElement element, string path)
    {
        if (element.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
        {
            throw new InvalidDataException(path + " must be a boolean.");
        }

        return element.GetBoolean();
    }

    private static int RequirePositiveInt32(JsonElement element, string path)
    {
        if (element.ValueKind != JsonValueKind.Number ||
            !element.TryGetInt32(out var value) ||
            value < 1)
        {
            throw new InvalidDataException(
                path + " must be a positive 32-bit integer.");
        }

        return value;
    }

    private static string RequiredPropertyString(
        JsonElement element,
        string name,
        string path)
    {
        if (!element.TryGetProperty(name, out var value))
        {
            throw new InvalidDataException(path + "." + name + " is required.");
        }

        var result = RequireString(value, path + "." + name);
        if (string.IsNullOrWhiteSpace(result))
        {
            throw new InvalidDataException(path + "." + name + " cannot be empty.");
        }

        return result;
    }

    private static void EnsureUniqueAndAllowed(
        JsonElement element,
        IEnumerable<string> allowed,
        string path)
    {
        var allowedSet = allowed.ToImmutableHashSet(StringComparer.Ordinal);
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var property in element.EnumerateObject())
        {
            if (!seen.Add(property.Name))
            {
                throw new InvalidDataException(
                    "Duplicate property " + path + "." + property.Name);
            }

            if (!allowedSet.Contains(property.Name))
            {
                throw new InvalidDataException(
                    "Unknown property " + path + "." + property.Name);
            }
        }
    }

    private static Presence<string> FindPolicy(string inputPath)
    {
        var fullInputPath = Path.GetFullPath(inputPath);
        Presence<DirectoryInfo> directory = Directory.Exists(fullInputPath)
            ? Presence.Of(new DirectoryInfo(fullInputPath))
            : new FileInfo(fullInputPath).Directory is DirectoryInfo parent
                ? Presence.Of(parent)
                : Presence.Missing<DirectoryInfo>();

        while (directory is Presence<DirectoryInfo>.Present current)
        {
            var candidate = Path.Combine(current.Value.FullName, ".csassay.json");
            if (File.Exists(candidate))
            {
                return Presence.Of(candidate);
            }

            directory = current.Value.Parent is DirectoryInfo next
                ? Presence.Of(next)
                : Presence.Missing<DirectoryInfo>();
        }

        return Presence.Missing<string>();
    }

    private static PolicyLoadResult Failure(string code, string message) =>
        new(
            AssayPolicy.Observe,
            Presence.Missing<string>(),
            ImmutableArray.Create(new EvaluationFailure(
                code,
                message,
                "policy",
                Presence.Missing<string>())),
            UsedDefault: true);

    private static Regex MetadataNamePattern() => MetadataNameRegex;

    private static Regex TargetFrameworkPattern() => TargetFrameworkRegex;

    private static Regex RuleIdPattern() => RuleIdRegex;

    private static Regex Sha256Pattern() => Sha256Regex;
}
