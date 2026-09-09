using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using ModAnalyzers.Json;

namespace ModAnalyzers;

//TODO - check localizations by language (separate keys into a map by language, report all languages missing keys)
//Probably keys map to a list of languages, and then compare that to list of all languages that exist

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class LocalizationAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "STS001";    
    public const string NoLocId = "STS002";
    public const string CustomModelRuleId = "STS003";

    private const string BaseLibAbstracts = "BaseLib.Abstracts.Custom";
    private const string CustomModelInterface = "BaseLib.Abstracts.ICustomModel";
    private const string ModelLocInterface = "BaseLib.Abstracts.ILocalizationProvider";
    private const string CustomIdAttribute = "BaseLib.Utils.Attributes.CustomIDAttribute";
    

    //Required localization data
    private static readonly Dictionary<string, RequiredLocalization[]> NamedTypeLocData = new()
    {
        {
            "MegaCrit.Sts2.Core.Models.CardModel", //modeltype
            [new RequiredLocalization("cards") //file
                .Add("SYMBOLID.title", "SYMBOLNAME")  //required entries
                .Add("SYMBOLID.description")
            ]
        },
        {
            "MegaCrit.Sts2.Core.Models.CharacterModel",
            [new RequiredLocalization("characters")
                .Add("SYMBOLID.title", "The SYMBOLNAME")
                .Add("SYMBOLID.titleObject", "The SYMBOLNAME")
                .Add("SYMBOLID.description", "Character Selection\\nScreen Description")
                .Add("SYMBOLID.pronounObject", "him/her/it")
                .Add("SYMBOLID.possessiveAdjective", "his/her/its")
                .Add("SYMBOLID.pronounPossessive", "his/hers/its")
                .Add("SYMBOLID.pronounSubject", "he/she/it")
                .Add("SYMBOLID.goldMonologue", "Line spoken when obtaining a large amount of gold")
                .Add("SYMBOLID.eventDeathPrevention", "Co-op survival line")
                .Add("SYMBOLID.aromaPrinciple", "Lore")
                .Add("SYMBOLID.cardsModifierTitle", "__ Cards")
                .Add("SYMBOLID.cardsModifierDescription", "__ cards will now appear in rewards and shops.")
                .Add("SYMBOLID.banter.alive.endTurnPing", "Co-op hurry up end turn ping message")
                .Add("SYMBOLID.banter.dead.endTurnPing", "..."),
            new RequiredLocalization("ancients")
                .Add("THE_ARCHITECT.talk.SYMBOLID.0-0r.char", "I am angry at the architect")
                .Add("THE_ARCHITECT.talk.SYMBOLID.0-0r.next", "Continue")
                .Add("THE_ARCHITECT.talk.SYMBOLID.0-1r.ancient", "You die")
                .Add("THE_ARCHITECT.talk.SYMBOLID.0-attack", "Both")]
        },
        {
            "MegaCrit.Sts2.Core.Models.PotionModel",
            [new RequiredLocalization("potions")
                .Add("SYMBOLID.title", "SYMBOLNAME")
                .Add("SYMBOLID.description")]
        },
        {
            "MegaCrit.Sts2.Core.Models.PowerModel",
            [new RequiredLocalization("powers")
                .Add("SYMBOLID.title", "SYMBOLNAME")
                .Add("SYMBOLID.description")
                .Add("SYMBOLID.smartDescription")]
        },
        {
            "MegaCrit.Sts2.Core.Models.RelicModel",
            [new RequiredLocalization("relics")
                .Add("SYMBOLID.title", "SYMBOLNAME")
                .Add("SYMBOLID.description")
                .Add("SYMBOLID.flavor")]
        },
        {
            "MegaCrit.Sts2.Core.Models.AncientEventModel",
            [new RequiredLocalization("ancients")
                .Add("SYMBOLID.title", "SYMBOLNAME")
                .Add("SYMBOLID.epithet")
                .Add("SYMBOLID.talk.firstVisitEver.0-0.ancient", "First time greeting.")
                .Add("SYMBOLID.talk.ANY.0-0r.ancient", "Reusable generic greeting.")]
        },
        {
            "MegaCrit.Sts2.Core.Models.ActModel",
            [new RequiredLocalization("acts")
                .Add("SYMBOLID.title", "SYMBOLNAME")
            ]
        }
    };

    private static readonly Dictionary<string, RequiredLocalization[]> EnumLocData = new()
    {
        {
            "CardKeyword", //enum type name
            [
                new RequiredLocalization("card_keywords") //file
                    .Add("SYMBOLID.title", "NAME") //required entries
                    .Add("SYMBOLID.description", "Tooltip")
            ]
        }
    };

    private static readonly Dictionary<string, string[]> CodeLocalizationData = new()
    {
        { "ActLoc", [ "title" ] },
        { "CardModifierLoc", [ "title", "description" ] },
        { "CardLoc", [ "title", "description" ] },
        { "CharacterLoc", [] },
        { "EncounterLoc", [ "title", "loss" ] },
        { "ModifierLoc", [ "title", "description" ] },
        { "MonsterLoc", [ "name" ] },
        { "OrbLoc", [ "title", "description", "smartDescription" ] },
        { "PotionLoc", [ "title", "description" ] },
        { "PowerLoc", [ "title", "description", "smartDescription" ] },
        { "RelicLoc", [ "title", "description", "flavor" ] }
    };

    /// <summary>
    /// Method overrides that disable entries for specific models.
    /// </summary>
    private static readonly Dictionary<string, KeyValuePair<string, string>[]> OverrideIgnores = new()
    {
        {
            "MegaCrit.Sts2.Core.Models.PowerModel",
            [
                new("Title", "SYMBOLID.title"),
                new("Description", "SYMBOLID.description"),
                new("SmartDescriptionLocKey", "SYMBOLID.smartDescription")
            ]
        }
    };

    class RequiredLocalization(string filename)
    {
        public readonly string Filename = filename;
        public readonly Dictionary<string, string> RequiredKeys = [];

        public RequiredLocalization Add(string key, string defaultValue = "")
        {
            RequiredKeys.Add(key, defaultValue);
            return this;
        }
    }
    
    private static readonly LocalizableString Title = new LocalizableResourceString(nameof(Resources.STS001Title),
        Resources.ResourceManager, typeof(Resources));
    private static readonly LocalizableString NoLocTitle = new LocalizableResourceString(nameof(Resources.STS002Title),
        Resources.ResourceManager, typeof(Resources));
    private static readonly LocalizableString CustomModelTitle = new LocalizableResourceString(nameof(Resources.STS003Title),
        Resources.ResourceManager, typeof(Resources));

    private static readonly LocalizableString MessageFormat =
        new LocalizableResourceString(nameof(Resources.STS001MessageFormat), Resources.ResourceManager,
            typeof(Resources));
    private static readonly LocalizableString CustomModelFormat =
        new LocalizableResourceString(nameof(Resources.STS003MessageFormat), Resources.ResourceManager,
            typeof(Resources));

    private static readonly LocalizableString Description =
        new LocalizableResourceString(nameof(Resources.STS001Description), Resources.ResourceManager,
            typeof(Resources));
    private static readonly LocalizableString NoLocDescription =
        new LocalizableResourceString(nameof(Resources.STS002Description), Resources.ResourceManager,
            typeof(Resources));
    private static readonly LocalizableString CustomModelDescription =
        new LocalizableResourceString(nameof(Resources.STS003Description), Resources.ResourceManager,
            typeof(Resources));

    private const string Category = "Localization";

    private static readonly DiagnosticDescriptor Rule = new(DiagnosticId, Title, MessageFormat, Category,
        DiagnosticSeverity.Error, isEnabledByDefault: true, description: Description);
    private static readonly DiagnosticDescriptor NoLoc = new(NoLocId, NoLocTitle, NoLocDescription, Category,
        DiagnosticSeverity.Warning, isEnabledByDefault: true, customTags: "CompilationEnd");
    private static readonly DiagnosticDescriptor CustomModelRule = new(CustomModelRuleId, CustomModelTitle, CustomModelFormat, Category,
        DiagnosticSeverity.Warning, isEnabledByDefault: true, description: CustomModelDescription);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
        [Rule, NoLoc, CustomModelRule, LoggingDiagnostic.Fake];

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        
        context.RegisterCompilationStartAction(LoadLocOnce);
    }

    private HashSet<string>? _currentLocKeys;

    private void LoadLocOnce(CompilationStartAnalysisContext context)
    {
        ImmutableArray<AdditionalText> additionalFiles = context.Options.AdditionalFiles;
        AdditionalText? aliasFile = additionalFiles.SingleOrDefault(file => file.Path.EndsWith("locAliases.json"));

        Dictionary<string, string> aliases = [];

        if (aliasFile is not null)
        {
            string jsonString = aliasFile.GetText()?.ToString() ?? "";
            List<LocAliasInfo> aliasInfo = JsonSerializer.Deserialize<List<LocAliasInfo>>(jsonString) ?? [];
            foreach (LocAliasInfo info in aliasInfo)
            {
                foreach (string alias in info.AliasPaths)
                {
                    aliases.Add(alias, info.BasePath);
                }
            }
        }

        _currentLocKeys = [];
        bool receivedJson = false;
        
        foreach (AdditionalText? file in additionalFiles)
        {
            if (file == null || file == aliasFile)
            {
                continue;
            }

            string path = file.Path;
            if (!path.EndsWith(".json"))
            {
                continue;
            }

            if (!path.Contains("localization"))
            {
                continue;
            }

            receivedJson = true;

            string? jsonText = file.GetText()?.ToString();
            if (jsonText == null)
            {
                continue;
            }

            try
            {
                string fileKey = Path.GetFileNameWithoutExtension(path);
                JsonValue? loc = JsonValue.Parse(jsonText);
                if (loc is not JsonObject locObj)
                {
                    continue;
                }

                foreach (string s in locObj.Keys)
                {
                    if (aliases.TryGetValue(fileKey, out string value))
                    {
                        _currentLocKeys.Add($"{value}.{s}");
                    }
                    else
                    {
                        _currentLocKeys.Add($"{fileKey}.{s}");
                    }
                }
            }
            catch (Exception)
            {
                // ignored
            }
        }

        INamedTypeSymbol? customModelInterface = 
            context.Compilation.GetTypeByMetadataName(CustomModelInterface);
        INamedTypeSymbol? customLocInterface = 
            context.Compilation.GetTypeByMetadataName(ModelLocInterface);
        INamedTypeSymbol? idAttribute =
            context.Compilation.GetTypeByMetadataName(CustomIdAttribute);
        
        context.RegisterSymbolAction(
            context => CheckSymbol(context, customModelInterface, customLocInterface, idAttribute), 
            SymbolKind.NamedType);
        context.RegisterSymbolAction(CheckField, SymbolKind.Field);
        context.RegisterCompilationEndAction(endContext =>
        {
            if (receivedJson)
            {
                return;
            }

            Diagnostic diagnostic = Diagnostic.Create(NoLoc, null);
            endContext.ReportDiagnostic(diagnostic);
        });
    }

    private void CheckSymbol(SymbolAnalysisContext context, INamedTypeSymbol? customModel, INamedTypeSymbol? locProvider, INamedTypeSymbol? idAttribute)
    {
        if (_currentLocKeys == null)
        {
            return;
        }

        if (context.Symbol is not INamedTypeSymbol namedTypeSymbol)
        {
            return;
        }

        if (namedTypeSymbol.IsAbstract || namedTypeSymbol.IsStatic)
        {
            return;
        }

        Dictionary<string, string> missingKeys = [];
        
        foreach (KeyValuePair<string, RequiredLocalization[]> entry in NamedTypeLocData)
        {
            if (!namedTypeSymbol.ImplementsInterfaceOrBaseClass(entry.Key))
            {
                continue;
            }

            bool isCustomModel = namedTypeSymbol.ImplementsInterface(customModel);
            
            //Check for localization provided through alternative means
            List<string> ignoreKeys = [];
            if (OverrideIgnores.TryGetValue(entry.Key, out KeyValuePair<string, string>[]? overrideIgnores))
            {
                foreach (KeyValuePair<string, string> overrideIgnore in overrideIgnores)
                {
                    if (namedTypeSymbol.OverridesMethodOrProperty(entry.Key, overrideIgnore.Key))
                    {
                        ignoreKeys.Add(overrideIgnore.Value);
                    }
                }
            }

            ISet<string>? ignoreOnce = null; //Only ignored in first required loc;
                                          //secondary required loc is in a different file and so is not ignored.
            if (namedTypeSymbol.ImplementsInterface(locProvider))
            {
                ignoreOnce = FindAndGetLocalizationDeclaration(namedTypeSymbol, "SYMBOLID", context);
                context.Log("ProvidedLoc: " + (ignoreOnce == null ? "null" : string.Join(",", ignoreOnce)), namedTypeSymbol.Locations[0]);
            }
            
            if (!isCustomModel)
            {
                string customModelName = entry.Key;
                int index = customModelName.LastIndexOf('.');
                customModelName = BaseLibAbstracts + customModelName.Substring(index + 1);
                Diagnostic modelTypeDiagnostic = Diagnostic.Create(CustomModelRule,
                    namedTypeSymbol.Locations[0],
                    customModelName);
                context.ReportDiagnostic(modelTypeDiagnostic);
            }

            AttributeData? customIdAttribute = namedTypeSymbol.GetAttributes()
                .FirstOrDefault(attr => SymbolEqualityComparer.Default.Equals(idAttribute, attr.AttributeClass));
            
            string fullName = namedTypeSymbol.FullName();
            string prefix = fullName.GetPrefix();
            string id = customIdAttribute?.AttributeArgumentString(0) ?? (isCustomModel ? prefix : "") + namedTypeSymbol.Name.Slugify();
            
            foreach (RequiredLocalization requiredLoc in entry.Value)
            {
                missingKeys.Clear();
                
                foreach (KeyValuePair<string, string> locEntry in requiredLoc.RequiredKeys)
                {
                    if (ignoreKeys.Contains(locEntry.Key))
                    {
                        continue;
                    }

                    if (ignoreOnce != null && (ignoreOnce.Count == 0 || ignoreOnce.Contains(locEntry.Key)))
                    {
                        continue;
                    }

                    string key = ReplaceSpecial(locEntry.Key, id, namedTypeSymbol.Name);
                    if (_currentLocKeys.Contains($"{requiredLoc.Filename}.{key}"))
                    {
                        continue;
                    }

                    string result = ReplaceSpecial(locEntry.Value, id, namedTypeSymbol.Name);
                    missingKeys.Add(key, result);
                }

                ignoreOnce = null;

                if (missingKeys.Count == 0)
                {
                    continue;
                }

                ImmutableDictionary<string, string?>.Builder builder = ImmutableDictionary.CreateBuilder<string, string?>();
                //For future, list all necessary languages. eg "eng/cards.json, zhs/cards.json"
                builder.Add("LOCFILES", requiredLoc.Filename + ".json");
                foreach (KeyValuePair<string, string> missingKey in missingKeys)
                {
                    builder.Add(missingKey.Key, missingKey.Value);
                }
                
                Diagnostic diagnostic = Diagnostic.Create(Rule,
                    namedTypeSymbol.Locations[0],
                    builder.ToImmutable(),
                    JoinKeys(missingKeys), fullName);
                context.ReportDiagnostic(diagnostic);
            }
            return;
        }
    }

    private ISet<string>? FindAndGetLocalizationDeclaration(INamedTypeSymbol? symbol, string symbolId, SymbolAnalysisContext context)
    {
        while (symbol != null)
        {
            foreach (ISymbol member in symbol.GetMembers())
            {
                if (member is not IPropertySymbol || !member.IsOverride ||
                    !member.Name.Equals("Localization"))
                {
                    continue;
                }

                ImmutableArray<SyntaxReference> syntaxReferences = member.DeclaringSyntaxReferences;
                if (syntaxReferences.Length == 0)
                {
                    return null;
                }

                SyntaxNode? syntax = syntaxReferences[0].GetSyntax();
                syntax = syntax.FindPropertyGetter(context);
                if (syntax == null)
                {
                    return null;
                }

                return GetLocalizationKeys(syntax, symbolId, context);
            }

            symbol = symbol.BaseType;
        }

        return null;
    }

    /// <summary>
    /// Returns localization keys defined by a syntax node specifically for a property
    /// of type List(string, string). Returns an empty list if it could not be analyzed.
    /// </summary>
    /// <param name="syntax"></param>
    /// <param name="symbolId"></param>
    /// <param name="context"></param>
    /// <returns></returns>
    private ISet<string>? GetLocalizationKeys(SyntaxNode syntax, string symbolId, SymbolAnalysisContext context)
    {
        LiteralExpressionSyntax? nullReturn =
            syntax.FindChild<LiteralExpressionSyntax>(test => test.IsKind(SyntaxKind.NullLiteralExpression));
        if (nullReturn != null)
        {
            return null;
        }

        SyntaxNode? objectCreation = syntax.FindChild<ObjectCreationExpressionSyntax>();

        IEnumerable<SyntaxNode> collectionItems;
        if (objectCreation is ObjectCreationExpressionSyntax objectCreationSyntax)
        {
            string typeName = objectCreationSyntax.CreationTypeName();
            context.Log(typeName, syntax.GetLocation());
            //Special localization types provided by BaseLib
            if (CodeLocalizationData.TryGetValue(typeName, out string[]? locNames))
            {
                return locNames.Select(name => $"{symbolId}.{name}").ToImmutableHashSet();
            }
            
            //Check for collection initializer
            objectCreation = objectCreation.FindChild<ExpressionSyntax>(test => 
                    test.IsKind(SyntaxKind.CollectionInitializerExpression));
            collectionItems = objectCreation?.ChildNodes()
                .OfType<TupleExpressionSyntax>() ?? [];
        }
        else
        {
            CollectionExpressionSyntax? collectionExpression = syntax.FindChild<CollectionExpressionSyntax>();
            if (collectionExpression == null)
            {
                return ImmutableHashSet<string>.Empty;
            }

            collectionItems = collectionExpression.ChildNodes().OfType<CollectionElementSyntax>()
                .Select(element => element.FindChild<TupleExpressionSyntax>()).OfType<TupleExpressionSyntax>();
        }

        HashSet<string> results = [];
        foreach (SyntaxNode item in collectionItems)
        {
            LiteralExpressionSyntax? firstValue = item.FindChild<ArgumentSyntax>()
                ?.FindChild<LiteralExpressionSyntax>(test => test.IsKind(SyntaxKind.StringLiteralExpression));
            if (firstValue == null)
            {
                return ImmutableHashSet<string>.Empty;
            }

            results.Add($"{symbolId}.{firstValue.Token.ValueText}");
        }
        
        return results; 
    }

    private void CheckField(SymbolAnalysisContext context)
    {
        if (_currentLocKeys == null)
        {
            return;
        }

        if (context.Symbol is not IFieldSymbol fieldSymbol)
        {
            return;
        }

        if (!fieldSymbol.IsStatic || fieldSymbol.IsReadOnly)
        {
            return;
        }

        ImmutableArray<AttributeData> attributes = fieldSymbol.GetAttributes();
        AttributeData? enumAttr = null;
        foreach (AttributeData attr in attributes)
        {
            if ("CustomEnumAttribute".Equals(attr.AttributeClass?.Name))
            {
                enumAttr = attr;
            }
            else if ("KeywordPropertiesAttribute".Equals(attr.AttributeClass?.Name))
            {
            }
        }

        if (enumAttr != null)
        {
            string name = fieldSymbol.Name;
            INamedTypeSymbol? containingType = fieldSymbol.ContainingType;
            
            if (containingType == null)
            {
                return;
            }

            Dictionary<string, string> missingKeys = [];
            
            foreach (KeyValuePair<string, RequiredLocalization[]> entry in EnumLocData)
            {
                if (!fieldSymbol.Type.Name.Contains(entry.Key))
                {
                    continue;
                }

                if (enumAttr.ConstructorArguments.Length > 0)
                {
                    object? nameArg = enumAttr.ConstructorArguments[0].Value;
                    if (nameArg != null)
                    {
                        name = nameArg.ToString();
                    }
                }
                string prefix = containingType.FullName().GetPrefix();
                string id = prefix + name.ToUpperInvariant();
        
                foreach (RequiredLocalization requiredLoc in entry.Value)
                {
                    missingKeys.Clear();
            
                    foreach (KeyValuePair<string, string> locEntry in requiredLoc.RequiredKeys)
                    {
                        string key = ReplaceSpecial(locEntry.Key, id, name);
                        if (_currentLocKeys.Contains($"{requiredLoc.Filename}.{key}"))
                        {
                            continue;
                        }

                        string result = ReplaceSpecial(locEntry.Value, id, name);
                        missingKeys.Add(key, result);
                    }

                    if (missingKeys.Count == 0)
                    {
                        continue;
                    }

                    ImmutableDictionary<string, string?>.Builder builder = ImmutableDictionary.CreateBuilder<string, string?>();
                    //For future, list all necessary languages. eg "eng/cards.json, zhs/cards.json"
                    builder.Add("LOCFILES", requiredLoc.Filename + ".json");
                    foreach (KeyValuePair<string, string> missingKey in missingKeys)
                    {
                        builder.Add(missingKey.Key, missingKey.Value);
                    }
            
                    Diagnostic diagnostic = Diagnostic.Create(Rule,
                        fieldSymbol.Locations[0],
                        builder.ToImmutable(),
                        JoinKeys(missingKeys), name);
                    context.ReportDiagnostic(diagnostic);
                }
            }
        }
    }

    private static string ReplaceSpecial(string orig, string id, string name)
    {
        string result = orig.Replace("SYMBOLID", id);
        result = result.Replace("SYMBOLNAME", name);
        return result;
    }

    private static string JoinKeys<T, U>(IDictionary<T, U> dict)
    {
        StringBuilder sb = new();
        bool first = true;
        foreach (KeyValuePair<T, U> entry in dict)
        {
            if (first)
            {
                first = false;
            }
            else
            {
                sb.Append(", ");
            }

            sb.Append(entry.Key);
        }

        return sb.ToString();
    }
}