using CsAssay.Catalogue;
using CsAssay.SdkAdapter;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace CsAssay.Analyzers.Tests;

public sealed class MetadataIdentityTests
{
    [Fact]
    public void Nested_metadata_names_preserve_generic_arity_and_nesting()
    {
        var compilation = Compile("namespace Example { public class Outer<T> { public class Inner<U> { } } }");
        var symbol = compilation.GetTypeByMetadataName("Example.Outer`1+Inner`1") ?? throw new InvalidOperationException();
        Assert.Equal("Example.Outer`1+Inner`1", symbol.GetFullMetadataName());
        Assert.Equal("Example.Outer`1+Inner`1", symbol.GetFullMetadataName());
    }

    [Fact]
    public async Task Same_short_names_from_different_compilations_are_not_conflated()
    {
        var first = Compile("namespace First { public class Shared { } }").GetTypeByMetadataName("First.Shared") ?? throw new InvalidOperationException();
        var second = Compile("namespace Second { public class Shared { } }").GetTypeByMetadataName("Second.Shared") ?? throw new InvalidOperationException();
        var values = await Task.WhenAll(Enumerable.Range(0, 24).Select(i => Task.Run(() => (i % 2 == 0 ? first : second).GetFullMetadataName(), TestContext.Current.CancellationToken)));
        for (var index = 0; index < values.Length; index++) { Assert.Equal(index % 2 == 0 ? "First.Shared" : "Second.Shared", values[index]); }
    }

    [Fact]
    public async Task Structural_nullable_arguments_and_nullable_values_still_block()
    {
        var diagnostics = await AnalyzerTestHost.AnalyzeAsync("""
            using System.Collections.Immutable;
            public sealed record NullableData(ImmutableArray<string?> Maybe, ImmutableArray<string> Present, int? Number);
            """);
        Assert.Contains(diagnostics, d => d.Id == RuleIds.NullableCoreContract && d.GetMessage(System.Globalization.CultureInfo.InvariantCulture).Contains("Maybe", StringComparison.Ordinal));
        Assert.Contains(diagnostics, d => d.Id == RuleIds.NullableCoreContract && d.GetMessage(System.Globalization.CultureInfo.InvariantCulture).Contains("Number", StringComparison.Ordinal));
        Assert.DoesNotContain(diagnostics, d => d.Id == RuleIds.NullableCoreContract &&
            (d.GetMessage(System.Globalization.CultureInfo.InvariantCulture).Contains("nullable parameter Present", StringComparison.Ordinal) || d.GetMessage(System.Globalization.CultureInfo.InvariantCulture).StartsWith("NullableData.Present ", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task Aliased_mutable_collection_keeps_its_actual_metadata_identity()
    {
        var diagnostics = await AnalyzerTestHost.AnalyzeAsync("""
            using Mutable = System.Collections.Generic.List<string>;
            public sealed record Aliased(Mutable Items);
            """);
        Assert.Contains(diagnostics, d => d.Id == RuleIds.MutableCollectionExposure);
        Assert.DoesNotContain(diagnostics, d => d.Id == RuleIds.NullableCoreContract);
    }

    private static CSharpCompilation Compile(string source) => CSharpCompilation.Create("Identity",
        [CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.Preview))],
        [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)],
        new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
}
