using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace ModAnalyzers;

public static class LoggingDiagnostic
{
    private const bool ENABLED = false;
    
    public static readonly DiagnosticDescriptor Fake = new("STS999", "Log", "{0}", "Logging",
        DiagnosticSeverity.Warning, isEnabledByDefault: true, description: "Logged info.");
    
    public static void Log(this SymbolAnalysisContext context, string msg, Location? location = null)
    {
        if (ENABLED)
        {
            context.ReportDiagnostic(Diagnostic.Create(Fake, location, msg));
        }
    }
}