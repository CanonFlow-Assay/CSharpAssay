using CsAssay.Domain;
using Microsoft.CodeAnalysis;

namespace CsAssay.SdkAdapter;

public static class MetadataIdentity
{
    public static string GetFullMetadataName(this ISymbol symbol)
    {
        if (symbol is null)
        {
            throw new ArgumentNullException(nameof(symbol));
        }

        var parts = new Stack<string>();
        Presence<ISymbol> current = Presence.Of(symbol);

        while (current is Presence<ISymbol>.Present present &&
               !IsRootNamespace(present.Value))
        {
            var part = present.Value.MetadataName;
            if (present.Value.ContainingType is not null)
            {
                part = "+" + part;
            }

            parts.Push(part);
            current = present.Value.ContainingSymbol is ISymbol containing
                ? Presence.Of(containing)
                : Presence.Missing<ISymbol>();
        }

        return string.Concat(parts.Select((part, index) =>
            index > 0 && part[0] != '+' ? "." + part : part));
    }

    public static bool IsMetadataType(ITypeSymbol type, string fullMetadataName) =>
        string.Equals(
            type.OriginalDefinition.GetFullMetadataName(),
            fullMetadataName,
            StringComparison.Ordinal);

    public static bool IsOneOf(ITypeSymbol type)
    {
        var metadataName = type.OriginalDefinition.GetFullMetadataName();
        return metadataName.StartsWith("OneOf.OneOf`", StringComparison.Ordinal);
    }

    public static bool IsTaskLike(ITypeSymbol type)
    {
        var metadataName = type.OriginalDefinition.GetFullMetadataName();
        return string.Equals(metadataName, "System.Threading.Tasks.Task", StringComparison.Ordinal) ||
            string.Equals(metadataName, "System.Threading.Tasks.Task`1", StringComparison.Ordinal) ||
            string.Equals(metadataName, "System.Threading.Tasks.ValueTask", StringComparison.Ordinal) ||
            string.Equals(metadataName, "System.Threading.Tasks.ValueTask`1", StringComparison.Ordinal);
    }

    private static bool IsRootNamespace(ISymbol symbol) =>
        symbol is INamespaceSymbol namespaceSymbol && namespaceSymbol.IsGlobalNamespace;
}
