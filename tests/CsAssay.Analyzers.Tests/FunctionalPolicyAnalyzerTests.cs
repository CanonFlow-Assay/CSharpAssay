using CsAssay.Catalogue;
using Microsoft.CodeAnalysis;

namespace CsAssay.Analyzers.Tests;

public sealed class FunctionalPolicyAnalyzerTests
{
    [Fact]
    public async Task Configured_domain_glossary_advises_on_raw_primitive_parameters()
    {
        var diagnostics = await AnalyzerTestHost.AnalyzeAsync(
            """
            public readonly record struct CustomerId(System.Guid Value);

            public static class Checkout
            {
                public static void Raw(System.Guid customerId) { }
                public static void Typed(CustomerId customerId) { }
            }
            """,
            new Dictionary<string, string>
            {
                ["csassay_domain_primitives"] =
                    "CustomerId=@customerId"
            });

        Assert.Single(
            diagnostics,
            diagnostic => diagnostic.Id == RuleIds.PrimitiveObsession);
    }

    [Fact]
    public async Task Domain_glossary_is_required_for_primitive_guidance()
    {
        var diagnostics = await AnalyzerTestHost.AnalyzeAsync(
            """
            public static class Checkout
            {
                public static void Process(System.Guid customerId) { }
            }
            """);

        Assert.DoesNotContain(
            diagnostics,
            diagnostic => diagnostic.Id == RuleIds.PrimitiveObsession);
    }

    [Fact]
    public async Task Advises_on_restricted_behavior_only_type_shapes()
    {
        var diagnostics = await AnalyzerTestHost.AnalyzeAsync(
            """
            public interface IPriceStrategy
            {
                decimal Calculate(decimal value);
            }

            public interface IRepository
            {
                void Save();
            }
            """);

        Assert.Single(
            diagnostics,
            diagnostic => diagnostic.Id == RuleIds.FunctionCandidate);
    }

    [Fact]
    public async Task Advises_on_multiple_state_flags()
    {
        var diagnostics = await AnalyzerTestHost.AnalyzeAsync(
            """
            public sealed class Session
            {
                public bool IsPending { get; init; }
                public bool IsComplete { get; init; }
            }

            public sealed class Capability
            {
                public bool CanRead { get; init; }
            }
            """);

        Assert.Single(
            diagnostics,
            diagnostic => diagnostic.Id == RuleIds.StateFlags);
    }

    [Fact]
    public async Task Advises_only_on_restricted_accumulation_loops()
    {
        var diagnostics = await AnalyzerTestHost.AnalyzeAsync(
            """
            using System.Collections.Generic;

            public static class Projection
            {
                public static List<int> Map(IEnumerable<int> source)
                {
                    var result = new List<int>();
                    foreach (var value in source)
                    {
                        result.Add(value * 2);
                    }
                    return result;
                }

                public static int Sum(IEnumerable<int> source)
                {
                    var total = 0;
                    foreach (var value in source)
                    {
                        total += value;
                    }
                    return total;
                }
            }
            """);

        Assert.Single(
            diagnostics,
            diagnostic => diagnostic.Id == RuleIds.LoopPipelineOpportunity);
    }

    [Fact]
    public async Task Advises_on_expected_exception_at_owned_public_boundary()
    {
        var diagnostics = await AnalyzerTestHost.AnalyzeAsync(
            """
            public static class Email
            {
                public static string Parse(string value) =>
                    value.Length > 0
                        ? value
                        : throw new System.ArgumentException("empty", nameof(value));

                private static string Internal() =>
                    throw new System.InvalidOperationException();

                public static string Require(string value) =>
                    value ?? throw new System.ArgumentNullException(nameof(value));
            }
            """);

        Assert.Single(
            diagnostics,
            diagnostic => diagnostic.Id == RuleIds.CoreBoundaryException);
    }

    [Fact]
    public async Task Advises_when_ordinary_public_contract_leaks_mutable_collection()
    {
        var diagnostics = await AnalyzerTestHost.AnalyzeAsync(
            """
            using System.Collections.Generic;

            public sealed class Boundary
            {
                public List<string> Values { get; } = [];
            }

            public sealed record ImmutableCarrier(
                System.Collections.Immutable.ImmutableArray<string> Values);
            """);

        Assert.Single(
            diagnostics,
            diagnostic => diagnostic.Id == RuleIds.MutableShellLeakage);
    }

    [Fact]
    public async Task Reports_nullable_disable()
    {
        var diagnostics = await AnalyzerTestHost.AnalyzeAsync(
            """
            #nullable disable
            public sealed class Sample { }
            """);

        Assert.Contains(
            diagnostics,
            diagnostic => diagnostic.Id == RuleIds.NullableDisabled);
    }

    [Theory]
    [InlineData(NullableContextOptions.Disable)]
    [InlineData(NullableContextOptions.Warnings)]
    [InlineData(NullableContextOptions.Annotations)]
    public async Task Reports_incomplete_project_nullable_context(
        NullableContextOptions nullableContext)
    {
        var diagnostics = await AnalyzerTestHost.AnalyzeAsync(
            "public sealed class Sample { }",
            new Dictionary<string, string>(),
            nullableContext);

        Assert.Single(
            diagnostics,
            diagnostic => diagnostic.Id == RuleIds.NullableDisabled);
    }

    [Fact]
    public async Task Accepts_fully_enabled_project_nullable_context()
    {
        var diagnostics = await AnalyzerTestHost.AnalyzeAsync(
            "public sealed class Sample { }");

        Assert.DoesNotContain(
            diagnostics,
            diagnostic => diagnostic.Id == RuleIds.NullableDisabled);
    }

    [Fact]
    public async Task Null_is_the_first_offence_family()
    {
        var diagnostics = await AnalyzerTestHost.AnalyzeAsync(
            """
            public sealed class CoreService
            {
                public string? Name { get; init; }

                public string? Transform(string? input) => input;

                public string NullValue() => null!;

                public string DefaultValue() => default;

                private static bool BoundaryCheck(string? input) =>
                    input is null;
            }
            """);

        Assert.Contains(
            diagnostics,
            diagnostic => diagnostic.Id == RuleIds.NullForgiving);
        Assert.Equal(
            2,
            diagnostics.Count(diagnostic =>
                diagnostic.Id == RuleIds.NullValueIntroduction));
        Assert.True(
            diagnostics.Count(diagnostic =>
                diagnostic.Id == RuleIds.NullableCoreContract) >= 3);
    }

    [Fact]
    public async Task Null_pattern_check_does_not_introduce_null()
    {
        var diagnostics = await AnalyzerTestHost.AnalyzeAsync(
            """
            internal static class Boundary
            {
                internal static bool IsMissing(string? input) =>
                    input is null;

                internal static bool IsPresent(string? input) =>
                    input != null;
            }
            """);

        Assert.DoesNotContain(
            diagnostics,
            diagnostic => diagnostic.Id == RuleIds.NullValueIntroduction);
    }

    [Fact]
    public async Task Object_equality_null_checks_do_not_introduce_null()
    {
        var diagnostics = await AnalyzerTestHost.AnalyzeAsync(
            """
            public abstract class ValueObject
            {
                protected static bool EqualOperator(
                    ValueObject left,
                    ValueObject right)
                {
                    if (ReferenceEquals(left, null) ^ ReferenceEquals(right, null))
                    {
                        return false;
                    }

                    return object.Equals(left, null) || left.Equals(right);
                }
            }
            """);

        Assert.DoesNotContain(
            diagnostics,
            diagnostic => diagnostic.Id == RuleIds.NullValueIntroduction);
    }

    [Fact]
    public async Task Null_passed_to_an_arbitrary_method_is_an_introduction()
    {
        var diagnostics = await AnalyzerTestHost.AnalyzeAsync(
            """
            public static class Core
            {
                public static void Enter(string value) { }
                public static void Poison() => Enter(null);
            }
            """);

        Assert.Contains(
            diagnostics,
            diagnostic => diagnostic.Id == RuleIds.NullValueIntroduction);
    }

    [Fact]
    public async Task Internal_surface_is_not_a_public_nullable_contract()
    {
        var diagnostics = await AnalyzerTestHost.AnalyzeAsync(
            """
            internal sealed class InternalBoundary
            {
                public string? Read(string? input) => input;
            }
            """);

        Assert.DoesNotContain(
            diagnostics,
            diagnostic => diagnostic.Id == RuleIds.NullableCoreContract);
    }

    [Fact]
    public async Task Positional_record_property_is_part_of_the_public_contract()
    {
        var diagnostics = await AnalyzerTestHost.AnalyzeAsync(
            """
            public sealed record Envelope(string? Value);
            """);

        Assert.Contains(
            diagnostics,
            diagnostic => diagnostic.Id == RuleIds.NullableCoreContract);
    }

    [Fact]
    public async Task Inherited_framework_contract_is_not_redeclared_as_owned()
    {
        var diagnostics = await AnalyzerTestHost.AnalyzeAsync(
            """
            public interface IBoundary
            {
                string? Read(string? value);
            }

            public sealed class Boundary : IBoundary
            {
                public string? Read(string? value) => value;
                public override bool Equals(object? other) =>
                    other is Boundary;
                public override int GetHashCode() => 0;
            }
            """);

        Assert.Equal(
            2,
            diagnostics.Count(diagnostic =>
                diagnostic.Id == RuleIds.NullableCoreContract));
        Assert.All(
            diagnostics.Where(diagnostic =>
                diagnostic.Id == RuleIds.NullableCoreContract),
            diagnostic => Assert.Contains(
                "IBoundary",
                diagnostic.GetMessage(
                    System.Globalization.CultureInfo.InvariantCulture),
                StringComparison.Ordinal));
    }

    [Fact]
    public async Task Omitted_optional_null_does_not_introduce_source_null()
    {
        var diagnostics = await AnalyzerTestHost.AnalyzeAsync(
            """
            using System.Text.Json;

            public sealed record Message(string Value);

            public static class Boundary
            {
                public static string Write(Message value) =>
                    JsonSerializer.Serialize(value);
            }
            """);

        Assert.DoesNotContain(
            diagnostics,
            diagnostic => diagnostic.Id == RuleIds.NullValueIntroduction);
    }

    [Fact]
    public async Task Reports_mutable_record_surface()
    {
        var diagnostics = await AnalyzerTestHost.AnalyzeAsync(
            """
            using System.Collections.Generic;
            public sealed record Order
            {
                public string Id { get; set; } = "";
                public List<string> Lines { get; init; } = [];
            }
            """);

        Assert.Contains(
            diagnostics,
            diagnostic => diagnostic.Id == RuleIds.MutableSetter);
        Assert.Contains(
            diagnostics,
            diagnostic => diagnostic.Id == RuleIds.MutableCollectionExposure);
    }

    [Fact]
    public async Task Reports_positional_mutable_collection_surface()
    {
        var diagnostics = await AnalyzerTestHost.AnalyzeAsync(
            """
            using System.Collections.Generic;
            public sealed record Order(List<string> Lines);
            """);

        Assert.Contains(
            diagnostics,
            diagnostic => diagnostic.Id == RuleIds.MutableCollectionExposure);
    }

    [Fact]
    public async Task Accepts_immutable_record_surface()
    {
        var diagnostics = await AnalyzerTestHost.AnalyzeAsync(
            """
            using System.Collections.Immutable;
            public sealed record Order(
                string Id,
                ImmutableArray<string> Lines);
            """);

        Assert.DoesNotContain(
            diagnostics,
            diagnostic => diagnostic.Id is
                RuleIds.MutableSetter or
                RuleIds.MutableCollectionExposure);
    }

    [Fact]
    public async Task Reports_empty_catch_and_not_observable_handling()
    {
        var diagnostics = await AnalyzerTestHost.AnalyzeAsync(
            """
            using System;
            public static class Sample
            {
                public static void Bad()
                {
                    try { throw new InvalidOperationException(); }
                    catch { }
                }

                public static void Good()
                {
                    try { throw new InvalidOperationException(); }
                    catch (Exception exception) { Console.Error.WriteLine(exception); }
                }
            }
            """);

        Assert.Single(
            diagnostics,
            diagnostic => diagnostic.Id == RuleIds.SwallowedException);
    }

    [Fact]
    public async Task Reports_async_void_but_allows_event_handler()
    {
        var diagnostics = await AnalyzerTestHost.AnalyzeAsync(
            """
            using System;
            using System.Threading.Tasks;
            public sealed class Sample
            {
                public async void Bad() { await Task.Yield(); }
                public async void OnClick(object sender, EventArgs args)
                {
                    await Task.Yield();
                }
            }
            """);

        Assert.Single(
            diagnostics,
            diagnostic => diagnostic.Id == RuleIds.AsyncVoid);
    }

    [Fact]
    public async Task Reports_blocking_awaitables_in_async_flow()
    {
        var diagnostics = await AnalyzerTestHost.AnalyzeAsync(
            """
            using System.Threading.Tasks;
            public static class Sample
            {
                public static async Task<int> Bad(Task<int> input)
                {
                    input.Wait();
                    await Task.Yield();
                    return input.Result;
                }
            }
            """);

        Assert.Equal(
            2,
            diagnostics.Count(diagnostic => diagnostic.Id == RuleIds.BlockingAsync));
    }

    [Fact]
    public async Task Reports_csharp_assay_pragma()
    {
        var diagnostics = await AnalyzerTestHost.AnalyzeAsync(
            """
            #pragma warning disable CSAN0001
            public sealed class Sample { }
            #pragma warning restore CSAN0001
            """);

        Assert.Contains(
            diagnostics,
            diagnostic => diagnostic.Id == RuleIds.UnauthorizedSuppression);
    }

    [Fact]
    public async Task Oneof_extraction_requires_matching_semantic_guard()
    {
        var diagnostics = await AnalyzerTestHost.AnalyzeAsync(
            """
            namespace OneOf
            {
                public readonly struct OneOf<T0, T1>
                {
                    public bool IsT0 => true;
                    public T0 AsT0 => default!;
                }
            }

            public static class Sample
            {
                public static string Bad(OneOf.OneOf<string, int> value) =>
                    value.AsT0;

                public static string Good(OneOf.OneOf<string, int> value)
                {
                    if (value.IsT0)
                    {
                        return value.AsT0;
                    }

                    return "";
                }
            }
            """);

        Assert.Single(
            diagnostics,
            diagnostic => diagnostic.Id == RuleIds.UnguardedOneOfExtraction);
    }

    [Fact]
    public async Task Configured_closed_hierarchy_is_checked()
    {
        var diagnostics = await AnalyzerTestHost.AnalyzeAsync(
            """
            namespace Domain;

            public abstract record Outcome
            {
                protected Outcome() { }
            }

            public sealed record Good : Outcome;
            public sealed record Bad : Outcome;

            public static class Consumer
            {
                public static string Show(Outcome value) => value switch
                {
                    Good => "good",
                    _ => "other"
                };
            }
            """,
            new Dictionary<string, string>
            {
                ["csassay_closed_types"] = "Domain.Outcome"
            });

        Assert.Contains(
            diagnostics,
            diagnostic => diagnostic.Id == RuleIds.ExtensibleClosedHierarchy);
        Assert.Contains(
            diagnostics,
            diagnostic => diagnostic.Id == RuleIds.IncompleteClosedHierarchySwitch);
    }

    [Fact]
    public async Task Malformed_source_does_not_crash_analyzer()
    {
        var diagnostics = await AnalyzerTestHost.AnalyzeAsync(
            """
            public record Broken
            {
                public string Name { get; set;
                catch {
            """);

        Assert.DoesNotContain(
            diagnostics,
            diagnostic => diagnostic.Id == "AD0001");
    }
}
