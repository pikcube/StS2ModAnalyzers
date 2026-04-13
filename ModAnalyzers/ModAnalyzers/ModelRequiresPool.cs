using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace ModAnalyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class ModelRequiresPool : DiagnosticAnalyzer
{
    public const string DiagnosticId = "STS004";

    private static readonly string[] ModelAbstracts = [
        "BaseLib.Abstracts.CustomCardModel",
        "BaseLib.Abstracts.CustomPotionModel",
        "BaseLib.Abstracts.CustomRelicModel"
    ];
    
    
    private static readonly LocalizableString Title = new LocalizableResourceString(nameof(Resources.STS004Title),
        Resources.ResourceManager, typeof(Resources));
    private static readonly LocalizableString MessageFormat =
        new LocalizableResourceString(nameof(Resources.STS004MessageFormat), Resources.ResourceManager,
            typeof(Resources));
    private static readonly LocalizableString Description =
        new LocalizableResourceString(nameof(Resources.STS004Description), Resources.ResourceManager,
            typeof(Resources));

    private const string Category = "Usage";
    private static readonly DiagnosticDescriptor Rule = new(DiagnosticId, Title, MessageFormat, Category,
        DiagnosticSeverity.Warning, isEnabledByDefault: true, description: Description);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        
        context.RegisterSymbolAction(CheckForPool, SymbolKind.NamedType);
    }

    private void CheckForPool(SymbolAnalysisContext context)
    {
        if (context.Symbol is not INamedTypeSymbol namedTypeSymbol) return;
        if (namedTypeSymbol.IsAbstract || namedTypeSymbol.IsStatic) return;
        
        foreach (var modelType in ModelAbstracts)
        {
            if (!namedTypeSymbol.ImplementsInterfaceOrBaseClass(modelType)) continue;
            if (HasPoolAttribute(namedTypeSymbol)) return;
            
            var diagnostic = Diagnostic.Create(Rule,
                namedTypeSymbol.Locations[0],
                namedTypeSymbol.FullName());
            context.ReportDiagnostic(diagnostic);
            
            return;
        }
    }

    private static bool HasPoolAttribute(INamedTypeSymbol namedTypeSymbol)
    {
        foreach (var attr in namedTypeSymbol.GetAttributes())
        {
            if ("PoolAttribute".Equals(attr.AttributeClass?.Name))
            {
                return true;
            }
        }
        return namedTypeSymbol.BaseType is not null && HasPoolAttribute(namedTypeSymbol.BaseType);
    }
}