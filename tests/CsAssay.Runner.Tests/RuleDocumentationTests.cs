using System.Collections.Immutable;
using CsAssay.Analyzers;
using CsAssay.Catalogue;
using CsAssay.Domain;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace CsAssay.Runner.Tests;

public sealed class RuleDocumentationTests
{
    private static readonly ImmutableArray<MetadataReference> References =
        ((string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") ?? throw new InvalidOperationException("Runtime references unavailable."))
        .Split(Path.PathSeparator).Select(p => MetadataReference.CreateFromFile(p)).ToImmutableArray<MetadataReference>();

    [Fact] public async Task Every_catalogue_id_has_offline_explanation_and_explicit_status()
    {
        foreach (var rule in RuleCatalogue.All)
        {
            using var output = new StringWriter(); using var error = new StringWriter();
            Assert.Equal(0, await CommandApp.RunAsync(["explain", rule.Id], output, error, TestContext.Current.CancellationToken));
            Assert.Contains(RuleDocumentation.Read(rule), output.ToString());
            Assert.Contains("Status: " + rule.Status, output.ToString());
            if (rule.Status == RuleStatus.Prototype) Assert.Contains("Prototype/advisory: nonblocking", output.ToString());
            Assert.Empty(error.ToString());
        }
    }

    [Fact] public async Task Admitted_before_after_examples_compile_and_match_their_named_rule()
    {
        foreach (var rule in RuleCatalogue.All.Where(r => r.Status == RuleStatus.Admitted))
        {
            var examples = RuleDocumentation.Read(rule).Replace("\r\n", "\n", StringComparison.Ordinal)
                .Split("```csharp", StringSplitOptions.None).Skip(1).Select(p => p[..p.IndexOf("```", StringComparison.Ordinal)].Trim()).ToArray();
            Assert.Equal(2, examples.Length);
            for (var index = 0; index < examples.Length; index++)
            {
                var compilation = CSharpCompilation.Create("Guide", [CSharpSyntaxTree.ParseText(examples[index], new CSharpParseOptions(LanguageVersion.Preview), path: "Guide.cs", cancellationToken: TestContext.Current.CancellationToken)],
                    References, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
                Assert.Empty(compilation.GetDiagnostics(TestContext.Current.CancellationToken).Where(d => d.Severity == DiagnosticSeverity.Error));
                var findings = await compilation.WithAnalyzers([new FunctionalPolicyAnalyzer()], new AnalyzerOptions([])).GetAnalyzerDiagnosticsAsync(TestContext.Current.CancellationToken);
                if (index == 0) Assert.Contains(findings, d => d.Id == rule.Id);
                else Assert.DoesNotContain(findings, d => d.Id == rule.Id);
            }
        }
    }
}
