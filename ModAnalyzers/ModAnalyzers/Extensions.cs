using System;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace ModAnalyzers;

internal static class Extensions
{
    public static string FullName(this INamedTypeSymbol symbol)
    {
        var format =
            SymbolDisplayFormat.FullyQualifiedFormat.WithGlobalNamespaceStyle(SymbolDisplayGlobalNamespaceStyle
                .Omitted);
        string fullName = symbol.ContainingNamespace?.ToDisplayString(format) ?? string.Empty;
        if (fullName.Length > 0) fullName += ".";
        return fullName + symbol.Name;
    }
    
    public static bool ImplementsInterface(this INamedTypeSymbol typeSymbol, INamedTypeSymbol? interfaceSymbol)
    {
        return interfaceSymbol != null && typeSymbol.AllInterfaces.Contains(interfaceSymbol);
    }
    
    public static bool ImplementsInterfaceOrBaseClass(this INamedTypeSymbol typeSymbol, Type typeToCheck)
    {
        return typeSymbol.ImplementsInterfaceOrBaseClass(typeToCheck.Name);
    }

    public static bool ImplementsInterfaceOrBaseClass(this INamedTypeSymbol typeSymbol, string name)
    {
        if (typeSymbol.FullName() == name)
        {
            return true;
        }
        
        foreach (var @interface in typeSymbol.AllInterfaces)
        {
            if (@interface.FullName() == name)
            {
                return true;
            }
        }

        var baseType = typeSymbol.BaseType;
        while (baseType != null)
        {
            if (baseType.FullName() == name) return true;
            baseType = baseType.BaseType;
        }

        return false;
    }

    /// <summary>
    /// returns true if typeSymbol has an override for property/method baseName defined in baseType
    /// </summary>
    /// <param name="typeSymbol"></param>
    /// <param name="baseTypeName"></param>
    /// <param name="baseName"></param>
    /// <returns></returns>
    public static bool OverridesMethodOrProperty(this INamedTypeSymbol typeSymbol, string baseTypeName, string baseName)
    {
        if (typeSymbol.FullName() == baseTypeName) return false;
        
        foreach (var symbol in typeSymbol.GetMembers())
        {
            if (symbol.IsOverride && symbol.Name == baseName) return true;
        }
        
        var baseType = typeSymbol.BaseType;
        return baseType != null && baseType.OverridesMethodOrProperty(baseTypeName, baseName);
    }

    public static string? AttributeArgumentString(this AttributeData attr, int argIndex)
    {
        var args = attr.ConstructorArguments;
        return argIndex >= args.Length ? null : args[argIndex].Value?.ToString();
    }

    public static SyntaxNode? FindPropertyGetter(this SyntaxNode propertySyntax, SymbolAnalysisContext context)
    {
        //First check for accessor list setup with getter
        SyntaxNode? propertyGetter = propertySyntax.FindChild<AccessorListSyntax>();
        propertyGetter = propertyGetter?.FindChild<AccessorDeclarationSyntax>(syntax => syntax.IsKind(SyntaxKind.GetAccessorDeclaration));
        
        //Find arrow expression as child of getter or as direct child
        SyntaxNode? valueDefinition = (propertyGetter ?? propertySyntax).FindChild<ArrowExpressionClauseSyntax>(syntax =>
            syntax.ArrowToken.IsKind(SyntaxKind.EqualsGreaterThanToken));

        if (valueDefinition == null)
        {
            //No arrow expression, check for block syntax
            if (propertyGetter == null) return null;

            valueDefinition = propertyGetter.FindChild<BlockSyntax>()?.FindChild<ReturnStatementSyntax>();
        }


        return valueDefinition;
    }

    public static string CreationTypeName(this ExpressionSyntax expression)
    {
        if (!expression.IsKind(SyntaxKind.ObjectCreationExpression)) return "WRONG EXPRESSION KIND";

        var nameSyntax = expression.FindChild<QualifiedNameSyntax>() ?? expression;
        nameSyntax = nameSyntax.FindChild<IdentifierNameSyntax>();

        return nameSyntax is not IdentifierNameSyntax name ? "" : name.GetFirstToken().ValueText;
    }

    public static T? FindChild<T>(this SyntaxNode syntax, Predicate<T>? condition = null) where T : SyntaxNode
    {
        foreach (var child in syntax.ChildNodes())
        {
            if (child is T correctChild && condition?.Invoke(correctChild) != false) return correctChild;
        }

        return null;
    }

    private static readonly Regex CamelCaseRegex =
        new(pattern: "([A-Za-z0-9]|\\G(?!^))([A-Z])",
            options: RegexOptions.Compiled);

    private static readonly Regex SnakeCaseRegex =
        new(pattern: "(.*?)_([a-zA-Z0-9])",
            options: RegexOptions.Compiled);

    private static readonly Regex WhitespaceRegex =
        new(pattern: "\\s+",
            options: RegexOptions.Compiled);

    private static readonly Regex SpecialCharRegex =
        new(pattern: "[^A-Za-z0-9_]",
            options: RegexOptions.Compiled);

    public static string Slugify(this string txt)
    {
        string str = CamelCaseRegex.Replace(txt.Trim(), "$1_$2");
        string input = WhitespaceRegex.Replace(str.ToUpper(), "_");
        return SpecialCharRegex.Replace(input, "");
    }

    public const char PREFIX_SPLIT_CHAR = '-';

    public static string GetPrefix(this string fullName)
    {
        return $"{fullName.GetRootNamespace().ToUpperInvariant()}{PREFIX_SPLIT_CHAR}";
    }
    
    public static string GetRootNamespace(this string fullName)
    {
        var pos = fullName.IndexOf('.');
        return pos < 0 ? "" : fullName.Substring(0, pos);
    }
}