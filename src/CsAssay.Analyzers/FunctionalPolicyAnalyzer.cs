using System.Collections.Immutable;
using CsAssay.Catalogue;
using CsAssay.Domain;
using CsAssay.SdkAdapter;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace CsAssay.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class FunctionalPolicyAnalyzer : DiagnosticAnalyzer
{
    private static readonly ImmutableHashSet<string> MutableCollectionTypes =
        ImmutableHashSet.Create(
            StringComparer.Ordinal,
            "System.Collections.ArrayList",
            "System.Collections.Hashtable",
            "System.Collections.ICollection",
            "System.Collections.IList",
            "System.Collections.IDictionary",
            "System.Collections.Generic.List`1",
            "System.Collections.Generic.Dictionary`2",
            "System.Collections.Generic.HashSet`1",
            "System.Collections.Generic.ICollection`1",
            "System.Collections.Generic.IList`1",
            "System.Collections.Generic.IDictionary`2",
            "System.Collections.ObjectModel.Collection`1",
            "System.Collections.ObjectModel.ObservableCollection`1");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        DescriptorProvider.All;

    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(
            GeneratedCodeAnalysisFlags.Analyze |
            GeneratedCodeAnalysisFlags.ReportDiagnostics);

        context.RegisterCompilationAction(AnalyzeNullableCompilation);
        context.RegisterSyntaxNodeAction(
            AnalyzeNullableDirective,
            SyntaxKind.NullableDirectiveTrivia);
        context.RegisterSyntaxNodeAction(
            AnalyzeNullForgiving,
            SyntaxKind.SuppressNullableWarningExpression);
        context.RegisterSyntaxNodeAction(
            AnalyzePragmaSuppression,
            SyntaxKind.PragmaWarningDirectiveTrivia);
        context.RegisterSyntaxNodeAction(
            AnalyzeSuppressMessage,
            SyntaxKind.Attribute);
        context.RegisterSyntaxNodeAction(
            AnalyzeCatchClause,
            SyntaxKind.CatchClause);
        context.RegisterSyntaxNodeAction(
            AnalyzeSwitchExpression,
            SyntaxKind.SwitchExpression);
        context.RegisterSyntaxNodeAction(
            AnalyzeSwitchStatement,
            SyntaxKind.SwitchStatement);
        context.RegisterSyntaxNodeAction(
            AnalyzeForEachStatement,
            SyntaxKind.ForEachStatement);
        context.RegisterSyntaxNodeAction(
            AnalyzeThrowSyntax,
            SyntaxKind.ThrowStatement,
            SyntaxKind.ThrowExpression);

        context.RegisterSymbolAction(AnalyzeProperty, SymbolKind.Property);
        context.RegisterSymbolAction(AnalyzeMethod, SymbolKind.Method);
        context.RegisterSymbolAction(AnalyzeField, SymbolKind.Field);
        context.RegisterSymbolAction(AnalyzeEvent, SymbolKind.Event);
        context.RegisterSymbolAction(AnalyzeClosedType, SymbolKind.NamedType);
        context.RegisterSymbolAction(AnalyzeGuidanceType, SymbolKind.NamedType);

        context.RegisterOperationAction(
            AnalyzePropertyReference,
            OperationKind.PropertyReference);
        context.RegisterOperationAction(
            AnalyzeInvocation,
            OperationKind.Invocation);
        context.RegisterOperationAction(
            AnalyzeNullLiteral,
            OperationKind.Literal);
        context.RegisterOperationAction(
            AnalyzeDefaultValue,
            OperationKind.DefaultValue);
    }

    private static void AnalyzeNullableCompilation(
        CompilationAnalysisContext context)
    {
        if (context.Compilation.Options is not CSharpCompilationOptions options ||
            options.NullableContextOptions == NullableContextOptions.Enable)
        {
            return;
        }

        Report(
            context,
            RuleIds.NullableDisabled,
            Location.None,
            "compilation nullable context is " +
                options.NullableContextOptions.ToString());
    }

    private static void AnalyzeNullableDirective(SyntaxNodeAnalysisContext context)
    {
        var directive = (NullableDirectiveTriviaSyntax)context.Node;
        if (!directive.SettingToken.IsKind(SyntaxKind.DisableKeyword))
        {
            return;
        }

        Report(
            context,
            RuleIds.NullableDisabled,
            directive.GetLocation(),
            "#nullable disable removes required compiler evidence");
    }

    private static void AnalyzeNullForgiving(SyntaxNodeAnalysisContext context)
    {
        Report(
            context,
            RuleIds.NullForgiving,
            context.Node.GetLocation(),
            "null-forgiving operator discards nullable-flow evidence");
    }

    private static void AnalyzePragmaSuppression(SyntaxNodeAnalysisContext context)
    {
        var directive = (PragmaWarningDirectiveTriviaSyntax)context.Node;
        if (!directive.DisableOrRestoreKeyword.IsKind(SyntaxKind.DisableKeyword))
        {
            return;
        }

        foreach (var code in directive.ErrorCodes)
        {
            var diagnosticId = code.ToString().Trim();
            if (!diagnosticId.StartsWith("CSA", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            Report(
                context,
                RuleIds.UnauthorizedSuppression,
                code.GetLocation(),
                "pragma disables " + diagnosticId + " without runner authorization");
        }
    }

    private static void AnalyzeSuppressMessage(SyntaxNodeAnalysisContext context)
    {
        var attribute = (AttributeSyntax)context.Node;
        if (context.SemanticModel.GetSymbolInfo(
                attribute,
                context.CancellationToken).Symbol is not IMethodSymbol constructor ||
            !string.Equals(
                constructor.ContainingType.GetFullMetadataName(),
                "System.Diagnostics.CodeAnalysis.SuppressMessageAttribute",
                StringComparison.Ordinal))
        {
            return;
        }

        if (attribute.ArgumentList is not AttributeArgumentListSyntax argumentList ||
            argumentList.Arguments.Count < 2)
        {
            return;
        }

        var value = context.SemanticModel.GetConstantValue(
            argumentList.Arguments[1].Expression,
            context.CancellationToken);
        if (!value.HasValue ||
            value.Value is not string checkId ||
            !checkId.StartsWith("CSA", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        Report(
            context,
            RuleIds.UnauthorizedSuppression,
            attribute.GetLocation(),
            "SuppressMessage targets " + checkId + " without runner authorization");
    }

    private static void AnalyzeCatchClause(SyntaxNodeAnalysisContext context)
    {
        var catchClause = (CatchClauseSyntax)context.Node;
        if (catchClause.Block.Statements.Count != 0)
        {
            return;
        }

        Report(
            context,
            RuleIds.SwallowedException,
            catchClause.CatchKeyword.GetLocation(),
            "empty catch block discards the failure");
    }

    private static void AnalyzeProperty(SymbolAnalysisContext context)
    {
        var property = (IPropertySymbol)context.Symbol;
        if (property.IsImplicitlyDeclared &&
            !property.Locations.Any(location => location.IsInSource))
        {
            return;
        }

        ReportNullableContract(context, property, property.Type);

        if (IsPublicApi(property) &&
            !property.ContainingType.IsRecord &&
            !ImplementsInheritedContract(property) &&
            IsKnownMutableCollection(property.Type))
        {
            Report(
                context,
                RuleIds.MutableShellLeakage,
                FirstSourceLocation(property),
                property.ToDisplayString() + " exposes " +
                    property.Type.ToDisplayString());
        }

        if (
            property.DeclaredAccessibility != Accessibility.Public ||
            !property.ContainingType.IsRecord)
        {
            return;
        }

        if (property.SetMethod is { IsInitOnly: false } setter &&
            setter.DeclaredAccessibility == Accessibility.Public)
        {
            Report(
                context,
                RuleIds.MutableSetter,
                FirstSourceLocation(property),
                property.ToDisplayString() + " exposes a public set accessor");
        }

        if (property.Type.TypeKind == TypeKind.Array ||
            MutableCollectionTypes.Contains(
                property.Type.OriginalDefinition.GetFullMetadataName()))
        {
            Report(
                context,
                RuleIds.MutableCollectionExposure,
                FirstSourceLocation(property),
                property.ToDisplayString() + " exposes " +
                    property.Type.ToDisplayString());
        }

    }

    private static void AnalyzeMethod(SymbolAnalysisContext context)
    {
        var method = (IMethodSymbol)context.Symbol;
        AnalyzeConfiguredDomainPrimitives(context, method);

        if (IsPublicApi(method) && !ImplementsInheritedContract(method))
        {
            if (!method.ReturnsVoid && ContainsNullableValue(method.ReturnType))
            {
                Report(
                    context,
                    RuleIds.NullableCoreContract,
                    FirstSourceLocation(method),
                    method.ToDisplayString() + " has a nullable return contract");
            }

            foreach (var parameter in method.Parameters.Where(parameter =>
                         ContainsNullableValue(parameter.Type)))
            {
                Report(
                    context,
                    RuleIds.NullableCoreContract,
                    FirstSourceLocation(parameter),
                    method.ToDisplayString() + " has nullable parameter " +
                        parameter.Name);
            }

            if (method.MethodKind == MethodKind.Ordinary &&
                !method.ReturnsVoid &&
                IsKnownMutableCollection(method.ReturnType))
            {
                Report(
                    context,
                    RuleIds.MutableShellLeakage,
                    FirstSourceLocation(method),
                    method.ToDisplayString() + " returns " +
                        method.ReturnType.ToDisplayString());
            }
        }

        if (!method.IsAsync || !method.ReturnsVoid || IsEventHandler(method))
        {
            return;
        }

        Report(
            context,
            RuleIds.AsyncVoid,
            FirstSourceLocation(method),
            method.ToDisplayString() + " is async void but is not an event handler");
    }

    private static void AnalyzeField(SymbolAnalysisContext context)
    {
        var field = (IFieldSymbol)context.Symbol;
        ReportNullableContract(context, field, field.Type);
    }

    private static void AnalyzeEvent(SymbolAnalysisContext context)
    {
        var @event = (IEventSymbol)context.Symbol;
        ReportNullableContract(context, @event, @event.Type);
    }

    private static void AnalyzeClosedType(SymbolAnalysisContext context)
    {
        var type = (INamedTypeSymbol)context.Symbol;
        if (!IsConfiguredClosedType(
                type,
                context.Options.AnalyzerConfigOptionsProvider))
        {
            return;
        }

        var externallyCallableConstructor = type.InstanceConstructors.FirstOrDefault(
            constructor => constructor.DeclaredAccessibility is
                Accessibility.Public or
                Accessibility.Protected or
                Accessibility.ProtectedOrInternal);

        if (externallyCallableConstructor is null)
        {
            return;
        }

        Report(
            context,
            RuleIds.ExtensibleClosedHierarchy,
            FirstSourceLocation(externallyCallableConstructor),
            type.ToDisplayString() + " has an externally callable constructor");
    }

    private static void AnalyzeGuidanceType(SymbolAnalysisContext context)
    {
        var type = (INamedTypeSymbol)context.Symbol;
        if (type.IsImplicitlyDeclared ||
            !type.Locations.Any(location => location.IsInSource))
        {
            return;
        }

        var stateMembers = type.GetMembers()
            .Where(member => !member.IsStatic && member.DeclaredAccessibility != Accessibility.Private)
            .Where(member => member switch
            {
                IPropertySymbol property =>
                    property.Type.SpecialType == SpecialType.System_Boolean,
                IFieldSymbol field =>
                    !field.IsImplicitlyDeclared &&
                    field.Type.SpecialType == SpecialType.System_Boolean,
                _ => false
            })
            .Select(member => member.Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
        if (stateMembers.Length >= 2)
        {
            Report(
                context,
                RuleIds.StateFlags,
                FirstSourceLocation(type),
                type.ToDisplayString() + " owns boolean state members: " +
                    string.Join(", ", stateMembers));
        }

        if (IsBehaviorTypeCandidate(type))
        {
            Report(
                context,
                RuleIds.FunctionCandidate,
                FirstSourceLocation(type),
                type.ToDisplayString() +
                    " has a restricted strategy/visitor/builder shape");
        }
    }

    private static void AnalyzeConfiguredDomainPrimitives(
        SymbolAnalysisContext context,
        IMethodSymbol method)
    {
        if (method.MethodKind != MethodKind.Ordinary ||
            !IsPublicApi(method) ||
            !context.Options.AnalyzerConfigOptionsProvider.GlobalOptions.TryGetValue(
                "csassay_domain_primitives",
                out var encoded) ||
            string.IsNullOrWhiteSpace(encoded))
        {
            return;
        }

        foreach (var entry in ParseDomainPrimitiveGlossary(encoded))
        {
            foreach (var parameter in method.Parameters.Where(parameter =>
                         string.Equals(
                             parameter.Name,
                             entry.ParameterName,
                             StringComparison.OrdinalIgnoreCase) &&
                         IsRawDomainPrimitive(parameter.Type) &&
                         !string.Equals(
                             parameter.Type.GetFullMetadataName(),
                             entry.ExpectedType,
                             StringComparison.Ordinal)))
            {
                Report(
                    context,
                    RuleIds.PrimitiveObsession,
                    FirstSourceLocation(parameter),
                    method.ToDisplayString() + " uses raw " +
                        parameter.Type.ToDisplayString() + " for " +
                        parameter.Name + "; glossary type is " +
                        entry.ExpectedType);
            }
        }
    }

    private static void AnalyzePropertyReference(OperationAnalysisContext context)
    {
        var operation = (IPropertyReferenceOperation)context.Operation;

        if (context.ContainingSymbol is IMethodSymbol
            {
                IsAsync: true
            } &&
            string.Equals(operation.Property.Name, "Result", StringComparison.Ordinal) &&
            MetadataIdentity.IsTaskLike(operation.Property.ContainingType))
        {
            Report(
                context,
                RuleIds.BlockingAsync,
                operation.Syntax.GetLocation(),
                operation.Property.ToDisplayString() + " blocks async flow");
        }

        if (!IsOneOfExtraction(operation) || HasMatchingOneOfGuard(operation))
        {
            return;
        }

        Report(
            context,
            RuleIds.UnguardedOneOfExtraction,
            operation.Syntax.GetLocation(),
            operation.Property.Name + " is outside the proven matching IsTn guard subset");
    }

    private static void AnalyzeInvocation(OperationAnalysisContext context)
    {
        var invocation = (IInvocationOperation)context.Operation;
        if (context.ContainingSymbol is not IMethodSymbol
            {
                IsAsync: true
            } ||
            !string.Equals(invocation.TargetMethod.Name, "Wait", StringComparison.Ordinal) ||
            !MetadataIdentity.IsTaskLike(invocation.TargetMethod.ContainingType))
        {
            return;
        }

        Report(
            context,
            RuleIds.BlockingAsync,
            invocation.Syntax.GetLocation(),
            invocation.TargetMethod.ToDisplayString() + " blocks async flow");
    }

    private static void AnalyzeNullLiteral(OperationAnalysisContext context)
    {
        var literal = (ILiteralOperation)context.Operation;
        if (literal.IsImplicit ||
            !literal.ConstantValue.HasValue ||
            literal.ConstantValue.Value is not null ||
            IsNullObservation(literal))
        {
            return;
        }

        Report(
            context,
            RuleIds.NullValueIntroduction,
            literal.Syntax.GetLocation(),
            "null is introduced as a value instead of an explicit domain case");
    }

    private static void AnalyzeDefaultValue(OperationAnalysisContext context)
    {
        var defaultValue = (IDefaultValueOperation)context.Operation;
        if (defaultValue.IsImplicit ||
            defaultValue.Type is not ITypeSymbol
            {
                IsReferenceType: true
            })
        {
            return;
        }

        Report(
            context,
            RuleIds.NullValueIntroduction,
            defaultValue.Syntax.GetLocation(),
            "reference-typed default introduces a null value");
    }

    private static void AnalyzeThrowSyntax(SyntaxNodeAnalysisContext context)
    {
        ExpressionSyntax expression;
        switch (context.Node)
        {
            case ThrowStatementSyntax
            {
                Expression: ExpressionSyntax statementExpression
            }:
                expression = statementExpression;
                break;
            case ThrowExpressionSyntax throwExpression:
                expression = throwExpression.Expression;
                break;
            default:
                return;
        }

        var methodDeclaration = context.Node.FirstAncestorOrSelf<MethodDeclarationSyntax>();
        if (methodDeclaration is null ||
            context.SemanticModel.GetDeclaredSymbol(
                methodDeclaration,
                context.CancellationToken) is not IMethodSymbol method ||
            !IsPublicApi(method) ||
            context.SemanticModel.GetTypeInfo(
                expression,
                context.CancellationToken).Type is not INamedTypeSymbol exceptionType ||
            !IsExpectedFailureException(exceptionType))
        {
            return;
        }

        Report(
            context,
            RuleIds.CoreBoundaryException,
            context.Node.GetLocation(),
            method.ToDisplayString() + " explicitly throws " +
                exceptionType.ToDisplayString());
    }

    private static void AnalyzeForEachStatement(SyntaxNodeAnalysisContext context)
    {
        var statement = (ForEachStatementSyntax)context.Node;
        if (!IsSimpleAccumulation(statement.Statement))
        {
            return;
        }

        Report(
            context,
            RuleIds.LoopPipelineOpportunity,
            statement.ForEachKeyword.GetLocation(),
            "simple foreach accumulation may be expressible as Select/Where");
    }

    private static void AnalyzeSwitchExpression(SyntaxNodeAnalysisContext context)
    {
        var switchExpression = (SwitchExpressionSyntax)context.Node;
        if (context.SemanticModel.GetTypeInfo(
            switchExpression.GoverningExpression,
            context.CancellationToken).Type is not INamedTypeSymbol switchedType)
        {
            return;
        }

        if (UnionCapabilities.IsNativeUnion(switchedType) &&
            switchExpression.Arms.Any(arm => arm.Pattern is DiscardPatternSyntax))
        {
            Report(
                context,
                RuleIds.NativeUnionDiscard,
                switchExpression.SwitchKeyword.GetLocation(),
                "discard arm can hide a newly added native union case");
        }

        AnalyzeClosedSwitch(
            context,
            switchedType,
            switchExpression.Arms.Select(arm => arm.Pattern),
            switchExpression.SwitchKeyword.GetLocation());
    }

    private static void AnalyzeSwitchStatement(SyntaxNodeAnalysisContext context)
    {
        var switchStatement = (SwitchStatementSyntax)context.Node;
        if (context.SemanticModel.GetTypeInfo(
            switchStatement.Expression,
            context.CancellationToken).Type is not INamedTypeSymbol switchedType)
        {
            return;
        }
        var labels = switchStatement.Sections.SelectMany(section => section.Labels);

        if (UnionCapabilities.IsNativeUnion(switchedType) &&
            labels.Any(label => label is DefaultSwitchLabelSyntax ||
                label is CasePatternSwitchLabelSyntax
                {
                    Pattern: DiscardPatternSyntax
                }))
        {
            Report(
                context,
                RuleIds.NativeUnionDiscard,
                switchStatement.SwitchKeyword.GetLocation(),
                "default/discard label can hide a newly added native union case");
        }

        AnalyzeClosedSwitch(
            context,
            switchedType,
            labels.OfType<CasePatternSwitchLabelSyntax>().Select(label => label.Pattern),
            switchStatement.SwitchKeyword.GetLocation());
    }

    private static void AnalyzeClosedSwitch(
        SyntaxNodeAnalysisContext context,
        INamedTypeSymbol switchedType,
        IEnumerable<PatternSyntax> patterns,
        Location location)
    {
        if (!IsConfiguredClosedType(
                switchedType,
                context.Options.AnalyzerConfigOptionsProvider))
        {
            return;
        }

        var directCases = EnumerateTypes(context.Compilation.Assembly.GlobalNamespace)
            .Where(type => SymbolEqualityComparer.Default.Equals(type.BaseType, switchedType))
            .ToImmutableArray();

        if (directCases.IsDefaultOrEmpty)
        {
            return;
        }

        var handledBuilder =
            ImmutableHashSet.CreateBuilder<INamedTypeSymbol>(
                SymbolEqualityComparer.Default);
        foreach (var pattern in patterns)
        {
            if (GetPatternType(pattern, context) is
                Presence<INamedTypeSymbol>.Present found)
            {
                handledBuilder.Add(found.Value);
            }
        }

        var handled = handledBuilder.ToImmutable();
        var missing = directCases
            .Where(type => !handled.Contains(type))
            .Select(type => type.ToDisplayString())
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        if (missing.Length == 0)
        {
            return;
        }

        Report(
            context,
            RuleIds.IncompleteClosedHierarchySwitch,
            location,
            "missing case(s): " + string.Join(", ", missing));
    }

    private static Presence<INamedTypeSymbol> GetPatternType(
        PatternSyntax pattern,
        SyntaxNodeAnalysisContext context)
    {
        var typeSyntax = pattern switch
        {
            TypePatternSyntax typePattern =>
                Presence.Of<TypeSyntax>(typePattern.Type),
            DeclarationPatternSyntax declarationPattern =>
                Presence.Of<TypeSyntax>(declarationPattern.Type),
            RecursivePatternSyntax
            {
                Type: TypeSyntax recursiveType
            } => Presence.Of(recursiveType),
            _ => Presence.Missing<TypeSyntax>()
        };

        if (typeSyntax is not Presence<TypeSyntax>.Present found ||
            context.SemanticModel.GetTypeInfo(
                found.Value,
                context.CancellationToken).Type is not INamedTypeSymbol type)
        {
            return Presence.Missing<INamedTypeSymbol>();
        }

        return Presence.Of(type);
    }

    private static bool IsEventHandler(IMethodSymbol method)
    {
        if (method.Parameters.Length != 2)
        {
            return false;
        }

        var eventArgs = method.Parameters[1].Type;
        while (eventArgs is not null)
        {
            if (string.Equals(
                    eventArgs.GetFullMetadataName(),
                    "System.EventArgs",
                    StringComparison.Ordinal))
            {
                return true;
            }

            eventArgs = eventArgs.BaseType;
        }

        return false;
    }

    private static void ReportNullableContract(
        SymbolAnalysisContext context,
        ISymbol symbol,
        ITypeSymbol type)
    {
        if (!IsPublicApi(symbol) ||
            ImplementsInheritedContract(symbol) ||
            !ContainsNullableValue(type))
        {
            return;
        }

        Report(
            context,
            RuleIds.NullableCoreContract,
            FirstSourceLocation(symbol),
            symbol.ToDisplayString() + " exposes a nullable value");
    }

    private static bool ContainsNullableValue(ITypeSymbol type)
    {
        if (type.NullableAnnotation == NullableAnnotation.Annotated)
        {
            return true;
        }

        if (type is IArrayTypeSymbol array)
        {
            return ContainsNullableValue(array.ElementType);
        }

        if (type is not INamedTypeSymbol named)
        {
            return false;
        }

        if (string.Equals(
                named.OriginalDefinition.GetFullMetadataName(),
                "System.Nullable`1",
                StringComparison.Ordinal))
        {
            return true;
        }

        return named.TypeArguments.Any(ContainsNullableValue);
    }

    private static bool IsPublicApi(ISymbol symbol)
    {
        if (!IsExternallyVisible(symbol.DeclaredAccessibility))
        {
            return false;
        }

        var containingType = symbol.ContainingType;
        while (containingType is not null)
        {
            if (!IsExternallyVisible(containingType.DeclaredAccessibility))
            {
                return false;
            }

            containingType = containingType.ContainingType;
        }

        return true;
    }

    private static bool IsExternallyVisible(Accessibility accessibility) =>
        accessibility is
            Accessibility.Public or
            Accessibility.Protected or
            Accessibility.ProtectedOrInternal;

    private static bool ImplementsInheritedContract(ISymbol symbol)
    {
        if (symbol is IMethodSymbol
            {
                IsOverride: true
            } or IMethodSymbol
            {
                ExplicitInterfaceImplementations.Length: > 0
            } ||
            symbol is IPropertySymbol
            {
                IsOverride: true
            } or IPropertySymbol
            {
                ExplicitInterfaceImplementations.Length: > 0
            } ||
            symbol is IEventSymbol
            {
                IsOverride: true
            } or IEventSymbol
            {
                ExplicitInterfaceImplementations.Length: > 0
            })
        {
            return true;
        }

        if (symbol.ContainingType is not INamedTypeSymbol containingType)
        {
            return false;
        }

        foreach (var @interface in containingType.AllInterfaces)
        {
            foreach (var member in @interface.GetMembers(symbol.Name))
            {
                if (SymbolEqualityComparer.Default.Equals(
                    containingType.FindImplementationForInterfaceMember(member),
                    symbol))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool IsNullObservation(ILiteralOperation literal)
    {
        IOperation? current = literal.Parent;
        while (current is IConversionOperation)
        {
            current = current.Parent;
        }

        return current is IConstantPatternOperation or
            IBinaryOperation
            {
                OperatorKind:
                    BinaryOperatorKind.Equals or
                    BinaryOperatorKind.NotEquals
            };
    }

    private static bool IsOneOfExtraction(IPropertyReferenceOperation operation)
    {
        if (!MetadataIdentity.IsOneOf(operation.Property.ContainingType) ||
            !operation.Property.Name.StartsWith("AsT", StringComparison.Ordinal))
        {
            return false;
        }

        return int.TryParse(
            operation.Property.Name.Substring(3),
            System.Globalization.NumberStyles.None,
            System.Globalization.CultureInfo.InvariantCulture,
            out _);
    }

    private static bool HasMatchingOneOfGuard(IPropertyReferenceOperation extraction)
    {
        var expectedGuard = "Is" + extraction.Property.Name.Substring(2);
        IOperation? current = extraction;

        while (current?.Parent is not null)
        {
            current = current.Parent;
            if (current is not IConditionalOperation conditional)
            {
                continue;
            }

            if (IsWithin(extraction, conditional.WhenTrue) &&
                ContainsGuard(
                    conditional.Condition,
                    expectedGuard,
                    extraction.Instance,
                    negated: false))
            {
                return true;
            }

            if (conditional.WhenFalse is not null &&
                IsWithin(extraction, conditional.WhenFalse) &&
                ContainsGuard(
                    conditional.Condition,
                    expectedGuard,
                    extraction.Instance,
                    negated: true))
            {
                return true;
            }
        }

        return false;
    }

    private static bool ContainsGuard(
        IOperation condition,
        string expectedGuard,
        IOperation? receiver,
        bool negated)
    {
        foreach (var candidate in DescendantsAndSelf(condition)
                     .OfType<IPropertyReferenceOperation>())
        {
            if (!string.Equals(
                    candidate.Property.Name,
                    expectedGuard,
                    StringComparison.Ordinal) ||
                !MetadataIdentity.IsOneOf(candidate.Property.ContainingType) ||
                !SameReceiver(receiver, candidate.Instance))
            {
                continue;
            }

            var isNegated = candidate.Parent is IUnaryOperation
            {
                OperatorKind: UnaryOperatorKind.Not
            };

            if (isNegated == negated)
            {
                return true;
            }
        }

        return false;
    }

    private static bool SameReceiver(IOperation? left, IOperation? right) =>
        (left, right) switch
        {
            (ILocalReferenceOperation leftLocal, ILocalReferenceOperation rightLocal) =>
                SymbolEqualityComparer.Default.Equals(leftLocal.Local, rightLocal.Local),
            (IParameterReferenceOperation leftParameter, IParameterReferenceOperation rightParameter) =>
                SymbolEqualityComparer.Default.Equals(
                    leftParameter.Parameter,
                    rightParameter.Parameter),
            (IFieldReferenceOperation leftField, IFieldReferenceOperation rightField) =>
                SymbolEqualityComparer.Default.Equals(leftField.Field, rightField.Field) &&
                SameReceiver(leftField.Instance, rightField.Instance),
            (IPropertyReferenceOperation leftProperty, IPropertyReferenceOperation rightProperty) =>
                SymbolEqualityComparer.Default.Equals(leftProperty.Property, rightProperty.Property) &&
                SameReceiver(leftProperty.Instance, rightProperty.Instance),
            (IInstanceReferenceOperation, IInstanceReferenceOperation) => true,
            _ => false
        };

    private static bool IsWithin(IOperation operation, IOperation container)
    {
        IOperation? current = operation;
        while (current is not null)
        {
            if (ReferenceEquals(current, container))
            {
                return true;
            }

            current = current.Parent;
        }

        return false;
    }

    private static IEnumerable<IOperation> DescendantsAndSelf(IOperation root)
    {
        var pending = new Stack<IOperation>();
        pending.Push(root);

        while (pending.Count > 0)
        {
            var current = pending.Pop();
            yield return current;

            foreach (var child in current.ChildOperations.Reverse())
            {
                pending.Push(child);
            }
        }
    }

    private static bool IsKnownMutableCollection(ITypeSymbol type) =>
        type.TypeKind == TypeKind.Array ||
        MutableCollectionTypes.Contains(
            type.OriginalDefinition.GetFullMetadataName());

    private static bool IsBehaviorTypeCandidate(INamedTypeSymbol type)
    {
        var ordinaryMethods = type.GetMembers()
            .OfType<IMethodSymbol>()
            .Where(method =>
                !method.IsStatic &&
                !method.IsImplicitlyDeclared &&
                method.MethodKind == MethodKind.Ordinary)
            .ToArray();

        if (type.TypeKind == TypeKind.Interface &&
            type.Name.EndsWith("Strategy", StringComparison.Ordinal) &&
            ordinaryMethods.Length == 1 &&
            !type.GetMembers().Any(member => member is IPropertySymbol or IEventSymbol))
        {
            return true;
        }

        if (type.TypeKind == TypeKind.Interface &&
            type.Name.EndsWith("Visitor", StringComparison.Ordinal) &&
            ordinaryMethods.Length >= 2)
        {
            return true;
        }

        return type.TypeKind == TypeKind.Class &&
            type.Name.EndsWith("Builder", StringComparison.Ordinal) &&
            ordinaryMethods.Any(method =>
                string.Equals(method.Name, "Build", StringComparison.Ordinal)) &&
            ordinaryMethods.All(method =>
                string.Equals(method.Name, "Build", StringComparison.Ordinal) ||
                method.Name.StartsWith("With", StringComparison.Ordinal) ||
                method.Name.StartsWith("Add", StringComparison.Ordinal) ||
                method.Name.StartsWith("Set", StringComparison.Ordinal));
    }

    private static IEnumerable<DomainPrimitiveGlossaryEntry>
        ParseDomainPrimitiveGlossary(string encoded)
    {
        foreach (var entry in encoded.Split(
                     [';'],
                     StringSplitOptions.RemoveEmptyEntries))
        {
            var separator = entry.IndexOf('=');
            if (separator <= 0 || separator == entry.Length - 1)
            {
                continue;
            }

            var expectedType = entry.Substring(0, separator).Trim();
            foreach (var parameterName in entry.Substring(separator + 1).Split(
                         [','],
                         StringSplitOptions.RemoveEmptyEntries))
            {
                var trimmed = parameterName.Trim();
                if (trimmed.Length > 0)
                {
                    yield return new DomainPrimitiveGlossaryEntry(
                        expectedType,
                        trimmed[0] == '@'
                            ? trimmed.Substring(1)
                            : trimmed);
                }
            }
        }
    }

    private static bool IsRawDomainPrimitive(ITypeSymbol type) =>
        type.SpecialType is
            SpecialType.System_String or
            SpecialType.System_Boolean or
            SpecialType.System_Byte or
            SpecialType.System_SByte or
            SpecialType.System_Int16 or
            SpecialType.System_UInt16 or
            SpecialType.System_Int32 or
            SpecialType.System_UInt32 or
            SpecialType.System_Int64 or
            SpecialType.System_UInt64 or
            SpecialType.System_Single or
            SpecialType.System_Double or
            SpecialType.System_Decimal or
            SpecialType.System_Char ||
        string.Equals(
            type.GetFullMetadataName(),
            "System.Guid",
            StringComparison.Ordinal);

    private static bool IsExpectedFailureException(INamedTypeSymbol type)
    {
        if (type.GetFullMetadataName() is
            "System.ArgumentNullException" or
            "System.ArgumentOutOfRangeException")
        {
            return false;
        }

        for (INamedTypeSymbol? current = type; current is not null; current = current.BaseType)
        {
            if (current.GetFullMetadataName() is
                "System.ArgumentException" or
                "System.InvalidOperationException" or
                "System.FormatException" or
                "System.Collections.Generic.KeyNotFoundException")
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsSimpleAccumulation(StatementSyntax statement)
    {
        var single = statement switch
        {
            BlockSyntax { Statements.Count: 1 } block => block.Statements[0],
            _ => statement
        };

        if (IsSingleAdd(single))
        {
            return true;
        }

        if (single is not IfStatementSyntax
            {
                Else: null
            } conditional)
        {
            return false;
        }

        return conditional.Statement switch
        {
            BlockSyntax { Statements.Count: 1 } block =>
                IsSingleAdd(block.Statements[0]),
            StatementSyntax nested => IsSingleAdd(nested)
        };
    }

    private static bool IsSingleAdd(StatementSyntax statement) =>
        statement is ExpressionStatementSyntax
        {
            Expression: InvocationExpressionSyntax
            {
                Expression: MemberAccessExpressionSyntax
                {
                    Name.Identifier.ValueText: "Add"
                },
                ArgumentList.Arguments.Count: 1
            }
        };

    private readonly record struct DomainPrimitiveGlossaryEntry(
        string ExpectedType,
        string ParameterName);

    private static bool IsConfiguredClosedType(
        INamedTypeSymbol type,
        AnalyzerConfigOptionsProvider optionsProvider)
    {
        if (!optionsProvider.GlobalOptions.TryGetValue(
                "csassay_closed_types",
                out var configured))
        {
            return false;
        }

        var metadataName = type.OriginalDefinition.GetFullMetadataName();
        return configured.Split([';', ','], StringSplitOptions.RemoveEmptyEntries)
            .Select(value => value.Trim())
            .Any(value => string.Equals(value, metadataName, StringComparison.Ordinal));
    }

    private static IEnumerable<INamedTypeSymbol> EnumerateTypes(
        INamespaceOrTypeSymbol container)
    {
        foreach (var member in container.GetMembers())
        {
            if (member is INamespaceSymbol namespaceSymbol)
            {
                foreach (var nested in EnumerateTypes(namespaceSymbol))
                {
                    yield return nested;
                }
            }
            else if (member is INamedTypeSymbol type)
            {
                yield return type;
                foreach (var nested in EnumerateTypes(type))
                {
                    yield return nested;
                }
            }
        }
    }

    private static Location FirstSourceLocation(ISymbol symbol) =>
        symbol.Locations.FirstOrDefault(location => location.IsInSource) ?? Location.None;

    private static void Report(
        SyntaxNodeAnalysisContext context,
        string ruleId,
        Location location,
        string detail) =>
        context.ReportDiagnostic(Diagnostic.Create(
            DescriptorProvider.Get(ruleId),
            location,
            detail));

    private static void Report(
        SymbolAnalysisContext context,
        string ruleId,
        Location location,
        string detail) =>
        context.ReportDiagnostic(Diagnostic.Create(
            DescriptorProvider.Get(ruleId),
            location,
            detail));

    private static void Report(
        OperationAnalysisContext context,
        string ruleId,
        Location location,
        string detail) =>
        context.ReportDiagnostic(Diagnostic.Create(
            DescriptorProvider.Get(ruleId),
            location,
            detail));

    private static void Report(
        CompilationAnalysisContext context,
        string ruleId,
        Location location,
        string detail) =>
        context.ReportDiagnostic(Diagnostic.Create(
            DescriptorProvider.Get(ruleId),
            location,
            detail));
}
