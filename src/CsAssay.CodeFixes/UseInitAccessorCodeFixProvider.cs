using System.Collections.Immutable;
using System.Composition;
using CsAssay.Catalogue;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CsAssay.CodeFixes;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(UseInitAccessorCodeFixProvider))]
[Shared]
public sealed class UseInitAccessorCodeFixProvider : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds =>
        [RuleIds.MutableSetter];

    public override FixAllProvider GetFixAllProvider() =>
        WellKnownFixAllProviders.BatchFixer;

    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document
            .GetSyntaxRootAsync(context.CancellationToken)
            .ConfigureAwait(false);
        if (root is null)
        {
            return;
        }

        var diagnostic = context.Diagnostics[0];
        var property = root.FindNode(diagnostic.Location.SourceSpan)
            .FirstAncestorOrSelf<PropertyDeclarationSyntax>();
        var setter = property?.AccessorList?.Accessors.FirstOrDefault(
            accessor => accessor.IsKind(SyntaxKind.SetAccessorDeclaration));

        if (setter is null || setter.Body is not null || setter.ExpressionBody is not null)
        {
            return;
        }

        context.RegisterCodeFix(
            CodeAction.Create(
                "Use init accessor",
                cancellationToken => ReplaceAsync(
                    context.Document,
                    root,
                    setter,
                    cancellationToken),
                equivalenceKey: "CsAssay.UseInitAccessor"),
            diagnostic);
    }

    private static Task<Document> ReplaceAsync(
        Document document,
        SyntaxNode root,
        AccessorDeclarationSyntax setter,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var initKeyword = SyntaxFactory.Token(
            setter.Keyword.LeadingTrivia,
            SyntaxKind.InitKeyword,
            setter.Keyword.TrailingTrivia);
        var replacement = setter.WithKeyword(initKeyword);
        var newRoot = root.ReplaceNode(setter, replacement);
        return Task.FromResult(document.WithSyntaxRoot(newRoot));
    }
}
