using System.Collections.Immutable;
using System.Reflection;
using System.Text.Json;
using CsAssay.Analyzers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace CsAssay.TypeGym;

internal static class CorpusVerifier
{
    private static readonly ImmutableArray<MetadataReference> References =
        CreateReferences();

    public static async Task VerifyAsync(string challenge)
    {
        var challengeRoot = Path.Combine(FindTypeGymRoot(), challenge);
        RequireFile(challengeRoot, "README.md");
        RequireFile(challengeRoot, "Challenge.Tests.cs");

        using var compatDocument = JsonDocument.Parse(
            File.ReadAllText(RequireFile(challengeRoot, "expected.compat.json")));
        var compat = compatDocument.RootElement;
        Assert.Equal(challenge, compat.GetProperty("challenge").GetString());

        var options = ReadOptions(compat);
        await VerifySourceAsync(
            RequireFile(challengeRoot, "Bad.cs"),
            compat.GetProperty("bad"),
            options);
        await VerifySourceAsync(
            RequireFile(challengeRoot, "Compat.cs"),
            compat.GetProperty("compat"),
            options);

        using var nativeDocument = JsonDocument.Parse(
            File.ReadAllText(RequireFile(challengeRoot, "expected.native.json")));
        var native = nativeDocument.RootElement;
        Assert.Equal(challenge, native.GetProperty("challenge").GetString());
        await VerifySourceAsync(
            RequireFile(challengeRoot, "Native.cs"),
            native.GetProperty("native"),
            ReadOptions(native));
    }

    private static async Task VerifySourceAsync(
        string path,
        JsonElement expectation,
        IReadOnlyDictionary<string, string> options)
    {
        var source = File.ReadAllText(path);
        var syntaxTree = CSharpSyntaxTree.ParseText(
            source,
            new CSharpParseOptions(LanguageVersion.CSharp14),
            path);
        var compilation = CSharpCompilation.Create(
            "TypeGym_" + Path.GetFileName(
                Path.GetDirectoryName(path) ?? throw new InvalidDataException(path)) +
            "_" + Path.GetFileNameWithoutExtension(path),
            [syntaxTree],
            References,
            new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                optimizationLevel: OptimizationLevel.Release,
                nullableContextOptions: NullableContextOptions.Enable));

        var compilerErrors = compilation.GetDiagnostics()
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .Select(diagnostic => diagnostic.ToString())
            .ToArray();
        Assert.True(
            compilerErrors.Length == 0,
            path + " failed compilation:" + Environment.NewLine +
            string.Join(Environment.NewLine, compilerErrors));

        var analyzerOptions = new AnalyzerOptions(
            ImmutableArray<AdditionalText>.Empty,
            new CorpusOptionsProvider(options));
        var diagnostics = await compilation.WithAnalyzers(
                [new FunctionalPolicyAnalyzer()],
                new CompilationWithAnalyzersOptions(
                    analyzerOptions,
                    onAnalyzerException: (_, _, _) => { },
                    concurrentAnalysis: true,
                    logAnalyzerExecutionTime: false,
                    reportSuppressedDiagnostics: true))
            .GetAnalyzerDiagnosticsAsync();
        var actualIds = diagnostics
            .Where(diagnostic => diagnostic.Id != "AD0001")
            .Select(diagnostic => diagnostic.Id)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToArray();
        var expectedIds = expectation.GetProperty("diagnostics")
            .EnumerateArray()
            .Select(element => element.GetString() ??
                throw new InvalidDataException(path + " has a non-string diagnostic ID."))
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(expectedIds, actualIds);
        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Id == "AD0001");

        using var image = new MemoryStream();
        var emit = compilation.Emit(image);
        Assert.True(
            emit.Success,
            path + " failed emission:" + Environment.NewLine +
            string.Join(Environment.NewLine, emit.Diagnostics));
        var assembly = Assembly.Load(image.ToArray());
        if (assembly.GetType("TypeGym.Challenge") is not Type probeType)
        {
            throw new InvalidDataException(
                path + " must define TypeGym.Challenge.");
        }

        if (probeType.GetMethod(
                "Probe",
                BindingFlags.Public | BindingFlags.Static) is not
            MethodInfo probe)
        {
            throw new InvalidDataException(
                path + " must define public static Probe().");
        }

        var actualGolden = probe.CreateDelegate<Func<string>>()();
        Assert.Equal(expectation.GetProperty("golden").GetString(), actualGolden);
    }

    private static Dictionary<string, string> ReadOptions(JsonElement root)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        if (!root.TryGetProperty("options", out var options))
        {
            return values;
        }

        foreach (var property in options.EnumerateObject())
        {
            values.Add(
                property.Name,
                property.Value.GetString() ??
                throw new InvalidDataException(
                    "Analyzer option " + property.Name + " must be a string."));
        }

        return values;
    }

    private static string RequireFile(string root, string name)
    {
        var path = Path.Combine(root, name);
        Assert.True(File.Exists(path), "Required corpus file is missing: " + path);
        return path;
    }

    private static string FindTypeGymRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            var candidate = Path.Combine(
                current.FullName,
                "typegym",
                "CsAssay.TypeGym");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException(
            "Could not locate typegym/CsAssay.TypeGym.");
    }

    private static ImmutableArray<MetadataReference> CreateReferences() =>
        ((string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") ??
            throw new InvalidOperationException(
                "Trusted platform assemblies are unavailable."))
        .Split(Path.PathSeparator)
        .Select(path => MetadataReference.CreateFromFile(path))
        .ToImmutableArray<MetadataReference>();

    private sealed class CorpusOptionsProvider(
        IReadOnlyDictionary<string, string> values) : AnalyzerConfigOptionsProvider
    {
        private static readonly AnalyzerConfigOptions Empty =
            new CorpusOptions(new Dictionary<string, string>());
        private readonly AnalyzerConfigOptions global = new CorpusOptions(values);

        public override AnalyzerConfigOptions GlobalOptions => global;

        public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) => Empty;

        public override AnalyzerConfigOptions GetOptions(AdditionalText textFile) => Empty;
    }

    private sealed class CorpusOptions(
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
