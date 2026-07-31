using System.Collections.Immutable;
using System.Text.Json;
using CsAssay.Analyzers;
using CsAssay.Catalogue;
using CsAssay.Domain;
using CsAssay.SdkAdapter;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace CsAssay.TypeGym;

public sealed class SpecimenClosureTests
{
    private static readonly ImmutableArray<MetadataReference> References =
        CreateReferences();

    [Fact]
    public async Task Every_catalogue_rule_has_semantic_corpus_closure()
    {
        var root = FindRepositoryRoot();
        using var document = JsonDocument.Parse(
            File.ReadAllText(Path.Combine(root, "specimens", "manifest.json")));
        var entries = document.RootElement.GetProperty("rules")
            .EnumerateArray()
            .ToArray();
        var manifestIds = entries
            .Select(entry => RequiredString(entry, "id"))
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToArray();
        var catalogueIds = RuleCatalogue.All
            .Select(rule => rule.Id)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(catalogueIds, manifestIds);

        foreach (var entry in entries)
        {
            var id = RequiredString(entry, "id");
            var capability = RequiredString(entry, "capability");
            var options = ReadOptions(entry);
            var rule = RuleCatalogue.Find(id) switch
            {
                CsAssay.Domain.Presence<RuleRecord>.Present found => found.Value,
                _ => throw new InvalidDataException("Unknown manifest rule " + id)
            };

            Assert.True(
                Directory.Exists(Path.Combine(root, rule.PositiveSpecimen)),
                "Missing positive specimen directory for " + id);
            Assert.True(
                Directory.Exists(Path.Combine(root, rule.NegativeSpecimen)),
                "Missing negative specimen directory for " + id);
            Assert.True(
                File.Exists(Path.Combine(root, rule.Documentation)),
                "Missing rule documentation for " + id);

            if (capability == "available")
            {
                var violation = await AnalyzeAsync(
                    Path.Combine(root, RequiredString(entry, "violation")),
                    options);
                Assert.Contains(violation, diagnostic => diagnostic.Id == id);
                Assert.DoesNotContain(
                    violation,
                    diagnostic => diagnostic.Id == "AD0001");

                var compliant = await AnalyzeAsync(
                    Path.Combine(root, RequiredString(entry, "compliant")),
                    options);
                Assert.DoesNotContain(compliant, diagnostic => diagnostic.Id == id);
                Assert.DoesNotContain(
                    compliant,
                    diagnostic => diagnostic.Id == "AD0001");

                var suppression = await AnalyzeAsync(
                    Path.Combine(root, RequiredString(entry, "suppression")),
                    options);
                Assert.Contains(
                    suppression,
                    diagnostic => diagnostic.Id == RuleIds.UnauthorizedSuppression);
            }
            else
            {
                Assert.Equal("native-preview-unavailable", capability);
                Assert.True(
                    File.Exists(Path.Combine(root, RequiredString(entry, "violation"))));
                Assert.True(
                    File.Exists(Path.Combine(root, RequiredString(entry, "compliant"))));
            }

            var malformed = await AnalyzeAsync(
                Path.Combine(root, RequiredString(entry, "fault")),
                options);
            Assert.DoesNotContain(
                malformed,
                diagnostic => diagnostic.Id == "AD0001");
            Assert.True(
                File.Exists(Path.Combine(
                    root,
                    "specimens",
                    "Faults",
                    id,
                    "analyzer-failure.json")));
        }
    }

    [Fact]
    public async Task Analyzer_exception_is_captured_as_tool_evidence()
    {
        var tree = CSharpSyntaxTree.ParseText(
            "public sealed class Sample { }",
            new CSharpParseOptions(LanguageVersion.CSharp14),
            cancellationToken: TestContext.Current.CancellationToken);
        var compilation = CSharpCompilation.Create(
            "ThrowingSpecimen",
            [tree],
            References,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        var result = await AnalyzerExecutor.ExecuteAsync(
            compilation,
            [CreateThrowingAnalyzer()],
            new AnalyzerOptions(ImmutableArray<AdditionalText>.Empty),
            TestContext.Current.CancellationToken);

        Assert.Single(result.Failures);
        Assert.Contains(
            "specimen analyzer failure",
            result.Failures[0].Message);
    }

    [Fact]
    public void Real_world_adjudication_format_covers_every_rule()
    {
        var root = FindRepositoryRoot();
        using var document = JsonDocument.Parse(
            File.ReadAllText(Path.Combine(
                root,
                "specimens",
                "RealWorld",
                "adjudication.json")));
        var ids = document.RootElement.GetProperty("rules")
            .EnumerateArray()
            .Select(entry => RequiredString(entry, "ruleId"))
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToArray();
        var expected = RuleCatalogue.All
            .Select(rule => rule.Id)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(expected, ids);
        Assert.Equal(
            0,
            document.RootElement.GetProperty("summary")
                .GetProperty("denominator")
                .GetInt32());
        Assert.Equal(
            "inconclusive",
            RequiredString(
                document.RootElement.GetProperty("summary"),
                "precision"));
    }

    private static async Task<ImmutableArray<Diagnostic>> AnalyzeAsync(
        string path,
        IReadOnlyDictionary<string, string> options)
    {
        Assert.True(File.Exists(path), "Missing specimen: " + path);
        var tree = CSharpSyntaxTree.ParseText(
            File.ReadAllText(path),
            new CSharpParseOptions(LanguageVersion.CSharp14),
            path);
        var compilation = CSharpCompilation.Create(
            "Specimen_" + Path.GetFileName(
                Path.GetDirectoryName(path) ??
                throw new InvalidDataException("Invalid specimen path.")),
            [tree],
            References,
            new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: NullableContextOptions.Enable));
        var analyzerOptions = new AnalyzerOptions(
            ImmutableArray<AdditionalText>.Empty,
            new SpecimenOptionsProvider(options));
        return await compilation.WithAnalyzers(
                [new FunctionalPolicyAnalyzer()],
                new CompilationWithAnalyzersOptions(
                    analyzerOptions,
                    onAnalyzerException: (_, _, _) => { },
                    concurrentAnalysis: true,
                    logAnalyzerExecutionTime: false,
                    reportSuppressedDiagnostics: true))
            .GetAnalyzerDiagnosticsAsync();
    }

    private static Dictionary<string, string> ReadOptions(JsonElement entry)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        if (!entry.TryGetProperty("options", out var options))
        {
            return result;
        }

        foreach (var property in options.EnumerateObject())
        {
            result.Add(
                property.Name,
                property.Value.GetString() ??
                throw new InvalidDataException(
                    property.Name + " must be a string."));
        }

        return result;
    }

    private static string RequiredString(JsonElement element, string property) =>
        element.GetProperty(property).GetString() ??
        throw new InvalidDataException(property + " must be a string.");

    private static string FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "CSharpAssay.slnx")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException(
            "Could not locate the CSharpAssay repository root.");
    }

    private static ImmutableArray<MetadataReference> CreateReferences() =>
        ((string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") ??
            throw new InvalidOperationException(
                "Trusted platform assemblies are unavailable."))
        .Split(Path.PathSeparator)
        .Select(path => MetadataReference.CreateFromFile(path))
        .ToImmutableArray<MetadataReference>();

    private static DiagnosticAnalyzer CreateThrowingAnalyzer()
    {
        const string source =
            """
            using System;
            using System.Collections.Immutable;
            using Microsoft.CodeAnalysis;
            using Microsoft.CodeAnalysis.Diagnostics;

            [DiagnosticAnalyzer(LanguageNames.CSharp)]
            public sealed class ThrowingSpecimenAnalyzer : DiagnosticAnalyzer
            {
                private static readonly DiagnosticDescriptor Descriptor = new(
                    "FAULT0001",
                    "Fault specimen",
                    "Fault specimen",
                    "Testing",
                    DiagnosticSeverity.Warning,
                    isEnabledByDefault: true);

                public override ImmutableArray<DiagnosticDescriptor>
                    SupportedDiagnostics => [Descriptor];

                public override void Initialize(AnalysisContext context)
                {
                    context.EnableConcurrentExecution();
                    context.ConfigureGeneratedCodeAnalysis(
                        GeneratedCodeAnalysisFlags.None);
                    context.RegisterCompilationAction(_ =>
                        throw new InvalidOperationException(
                            "specimen analyzer failure"));
                }
            }
            """;
        var tree = CSharpSyntaxTree.ParseText(
            source,
            new CSharpParseOptions(LanguageVersion.CSharp14));
        var compilation = CSharpCompilation.Create(
            "ThrowingSpecimenAnalyzerAssembly",
            [tree],
            References.Add(MetadataReference.CreateFromFile(
                typeof(DiagnosticAnalyzer).Assembly.Location)),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        using var image = new MemoryStream();
        var emit = compilation.Emit(image);
        Assert.True(
            emit.Success,
            string.Join(Environment.NewLine, emit.Diagnostics));
        var assembly = System.Reflection.Assembly.Load(image.ToArray());
        if (assembly.GetType("ThrowingSpecimenAnalyzer") is not Type analyzerType ||
            Activator.CreateInstance(analyzerType) is not DiagnosticAnalyzer analyzer)
        {
            throw new InvalidOperationException(
                "Could not construct the throwing analyzer specimen.");
        }

        return analyzer;
    }

    private sealed class SpecimenOptionsProvider(
        IReadOnlyDictionary<string, string> values) : AnalyzerConfigOptionsProvider
    {
        private static readonly AnalyzerConfigOptions Empty =
            new SpecimenOptions(new Dictionary<string, string>());
        private readonly AnalyzerConfigOptions global = new SpecimenOptions(values);

        public override AnalyzerConfigOptions GlobalOptions => global;

        public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) => Empty;

        public override AnalyzerConfigOptions GetOptions(AdditionalText textFile) => Empty;
    }

    private sealed class SpecimenOptions(
        IReadOnlyDictionary<string, string> values) : AnalyzerConfigOptions
    {
        public override bool TryGetValue(string key, out string value)
        {
            if (values.TryGetValue(key, out var found) &&
                found is string required)
            {
                value = required;
                return true;
            }

            value = string.Empty;
            return false;
        }
    }
}
