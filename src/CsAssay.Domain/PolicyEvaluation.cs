using System.Collections.Immutable;
using System.Globalization;
using System.Text.RegularExpressions;

namespace CsAssay.Domain;

public sealed record BoundaryDecision(string Scope, bool UsesCoreDisposition);

/// <summary>Normalized, pure policy evaluation shared by compiler and verifier hosts.</summary>
public sealed class PolicyEvaluation
{
    private readonly ImmutableHashSet<string> coreProjects;
    private readonly ImmutableHashSet<string> shellProjects;
    private readonly ImmutableDictionary<(string RuleId, string Fingerprint), SuppressionGrant> grants;

    private PolicyEvaluation(AssayPolicy policy, StringComparer pathComparer)
    {
        Policy = policy;
        coreProjects = policy.Boundaries.CoreProjects.Select(NormalizeProject).ToImmutableHashSet(pathComparer);
        shellProjects = policy.Boundaries.ShellProjects.Select(NormalizeProject).ToImmutableHashSet(pathComparer);
        if (coreProjects.Overlaps(shellProjects))
        {
            throw new InvalidDataException("A project selector cannot be both core and shell.");
        }

        grants = policy.Suppressions.ToImmutableDictionary(grant => (grant.RuleId, grant.Fingerprint));
    }

    public AssayPolicy Policy { get; }

    public static PolicyEvaluation Normalize(
        AssayPolicy policy,
        ImmutableArray<string> knownRuleIds,
        bool caseInsensitivePaths)
    {
        ValidateDeclarations(policy, knownRuleIds);
        return new PolicyEvaluation(policy, caseInsensitivePaths ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
    }

    public static void ValidateDeclarations(AssayPolicy policy, ImmutableArray<string> knownRuleIds)
    {
        ArgumentNullException.ThrowIfNull(policy);
        var known = knownRuleIds.ToImmutableHashSet(StringComparer.Ordinal);
        foreach (var selector in policy.Boundaries.CoreProjects.Concat(policy.Boundaries.ShellProjects))
        {
            var normalized = NormalizeProject(selector);
            if (string.IsNullOrWhiteSpace(normalized) || normalized.StartsWith('/') ||
                normalized.Contains(':', StringComparison.Ordinal))
            {
                throw new InvalidDataException("Project selectors must be literal policy-relative paths: " + selector);
            }
        }

        if (policy.Boundaries.CoreProjects.Select(NormalizeProject).Intersect(
                policy.Boundaries.ShellProjects.Select(NormalizeProject), StringComparer.Ordinal).Any())
        {
            throw new InvalidDataException("A project selector cannot be both core and shell.");
        }

        foreach (var selector in policy.Boundaries.CoreNamespaces.Concat(policy.Boundaries.ShellNamespaces))
        {
            if (!Regex.IsMatch(selector, @"^@?[_\p{L}\p{Nl}][_\p{L}\p{Nl}\p{Nd}\p{Pc}\p{Mn}\p{Mc}\p{Cf}]*(\.@?[_\p{L}\p{Nl}][_\p{L}\p{Nl}\p{Nd}\p{Pc}\p{Mn}\p{Mc}\p{Cf}]*)*$", RegexOptions.CultureInvariant))
            {
                throw new InvalidDataException("Invalid namespace selector: " + selector);
            }
        }

        if (policy.Boundaries.CoreNamespaces.Select(NormalizeNamespace).Intersect(policy.Boundaries.ShellNamespaces.Select(NormalizeNamespace), StringComparer.Ordinal).Any())
        {
            throw new InvalidDataException("A namespace selector cannot be both core and shell.");
        }

        var identities = new HashSet<(string RuleId, string Fingerprint)>();
        foreach (var grant in policy.Suppressions)
        {
            if (!known.Contains(grant.RuleId) || grant.RuleId == "CSAP0001")
            {
                throw new InvalidDataException("Suppression rule must be known and cannot be CSAP0001: " + grant.RuleId);
            }

            if (string.IsNullOrWhiteSpace(grant.Owner) || string.IsNullOrWhiteSpace(grant.Reason) ||
                !Regex.IsMatch(grant.Fingerprint, "^[0-9a-f]{64}$", RegexOptions.CultureInvariant))
            {
                throw new InvalidDataException("A grant requires an owner, concrete reason and lowercase SHA-256 fingerprint.");
            }

            if (!identities.Add((grant.RuleId, grant.Fingerprint)))
            {
                throw new InvalidDataException("Duplicate or conflicting suppression grant: " + grant.RuleId + "/" + grant.Fingerprint);
            }
        }
    }

    public BoundaryDecision Classify(string project, string namespaceName, bool hasSourceLocation, bool declaresCoreNamespace)
    {
        project = NormalizeProject(project);
        if (shellProjects.Contains(project)) { return new("shell", false); }
        var explicitCore = coreProjects.Contains(project);
        if (coreProjects.Count > 0 && !explicitCore) { return new("default", false); }
        if (hasSourceLocation && MatchesNamespace(namespaceName, Policy.Boundaries.ShellNamespaces))
        {
            return new("shell", false);
        }

        if (Policy.Boundaries.CoreNamespaces.IsDefaultOrEmpty)
        {
            return new(explicitCore ? "core" : "default", true);
        }

        var core = hasSourceLocation
            ? MatchesNamespace(namespaceName, Policy.Boundaries.CoreNamespaces)
            : explicitCore || declaresCoreNamespace;
        return new(core ? "core" : "default", core);
    }

    public bool RequiresNamespaceEvaluation(string project) =>
        !shellProjects.Contains(NormalizeProject(project)) &&
        (coreProjects.Count == 0 || coreProjects.Contains(NormalizeProject(project))) &&
        (!Policy.Boundaries.CoreNamespaces.IsDefaultOrEmpty || !Policy.Boundaries.ShellNamespaces.IsDefaultOrEmpty);

    public static RuleDisposition Disposition(string ruleId, RuleDisposition declared, BoundaryDecision boundary) =>
        ruleId == "CSAP0001" || boundary.UsesCoreDisposition ? declared : RuleDisposition.Advise;

    public Presence<SuppressionGrant> AcceptedGrant(string ruleId, string fingerprint, DateOnly utcEvaluationDate) =>
        ruleId != "CSAP0001" && grants.TryGetValue((ruleId, fingerprint), out var grant) &&
        DateOnly.FromDateTime(grant.Expires.UtcDateTime) >= utcEvaluationDate
            ? Presence.Of(grant)
            : Presence.Missing<SuppressionGrant>();

    public ImmutableArray<string> UnmatchedProjects(IEnumerable<string> evaluatedProjects)
    {
        var evaluated = evaluatedProjects.Select(NormalizeProject).ToImmutableHashSet(coreProjects.KeyComparer);
        return coreProjects.Concat(shellProjects).Where(selector => !evaluated.Contains(selector)).Order(StringComparer.Ordinal).ToImmutableArray();
    }

    public static bool MatchesNamespace(string name, ImmutableArray<string> selectors) =>
        selectors.Any(selector => NormalizeNamespace(name) == NormalizeNamespace(selector) ||
            NormalizeNamespace(name).StartsWith(NormalizeNamespace(selector) + ".", StringComparison.Ordinal));

    public static string EvaluationDateText(DateOnly date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static string NormalizeNamespace(string value) => !value.Contains('@', StringComparison.Ordinal) ? value :
        string.Join(".", value.Split('.').Select(part => part.StartsWith('@') ? part[1..] : part));

    private static string NormalizeProject(string value)
    {
        var path = Fingerprints.NormalizePath(value);
        if (path.StartsWith('/')) { return path; }
        var parts = new List<string>();
        foreach (var part in path.Split('/'))
        {
            if (part is "" or ".") { continue; }
            if (part == ".." && parts.Count > 0 && parts[^1] != "..") { parts.RemoveAt(parts.Count - 1); }
            else { parts.Add(part); }
        }
        return string.Join("/", parts);
    }
}
