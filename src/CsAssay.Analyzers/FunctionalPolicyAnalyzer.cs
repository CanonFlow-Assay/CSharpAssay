using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace CsAssay.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class FunctionalPolicyAnalyzer : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        DescriptorProvider.All;

    private readonly AnalysisPolicyContext? suppliedPolicy;

    public FunctionalPolicyAnalyzer() { }

    public FunctionalPolicyAnalyzer(AnalysisPolicyContext policy) => suppliedPolicy = policy;

    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterCompilationStartAction(start => new FunctionalPolicyRules(AnalysisPolicySession.Create(start.Compilation, start.Options, suppliedPolicy, start.CancellationToken)).Register(start));
    }

}
