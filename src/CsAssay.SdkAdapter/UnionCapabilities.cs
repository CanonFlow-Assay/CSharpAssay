using System.Reflection;
using Microsoft.CodeAnalysis;

namespace CsAssay.SdkAdapter;

public static class UnionCapabilities
{
    private static readonly string[] KnownUnionAttributes =
    [
        "System.Runtime.CompilerServices.UnionAttribute",
        "System.Diagnostics.CodeAnalysis.UnionAttribute"
    ];

    public static bool IsNativeUnion(ITypeSymbol type)
    {
        if (type.GetAttributes().Any(attribute =>
            attribute.AttributeClass is INamedTypeSymbol attributeClass &&
            KnownUnionAttributes.Contains(
                attributeClass.GetFullMetadataName(),
                StringComparer.Ordinal)))
        {
            return true;
        }

        if (type.AllInterfaces.Any(@interface =>
            string.Equals(
                @interface.GetFullMetadataName(),
                "System.Runtime.CompilerServices.IUnion",
                StringComparison.Ordinal)))
        {
            return true;
        }

        // Preview Roslyn has changed this surface more than once. Reflection keeps
        // that instability inside the SDK adapter and fails closed when absent.
        var property = type.GetType().GetProperty(
            "IsUnion",
            BindingFlags.Instance | BindingFlags.Public);

        return property is PropertyInfo unionProperty &&
            unionProperty.PropertyType == typeof(bool) &&
            unionProperty.GetValue(type) is true;
    }

    public static bool CompilerExposesUnionSymbols()
    {
        var property = typeof(INamedTypeSymbol).GetProperty(
            "IsUnion",
            BindingFlags.Instance | BindingFlags.Public);

        return property is PropertyInfo unionProperty &&
            unionProperty.PropertyType == typeof(bool);
    }
}
