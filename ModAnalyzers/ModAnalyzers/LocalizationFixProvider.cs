using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.Text;

namespace ModAnalyzers;


[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(LocalizationFixProvider)), Shared]
public class LocalizationFixProvider : CodeFixProvider
{
    public sealed override ImmutableArray<string> FixableDiagnosticIds { get; } =
        ImmutableArray.Create(LocalizationAnalyzer.DiagnosticId);

    public override FixAllProvider? GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        Dictionary<string, string?> missingKeys = [];
        string? locFiles = null;
        //Should be the same for all, as all diagnostics should share the same span.
        //Realistically should only apply to one file at a time.
        
        foreach (Diagnostic diagnostic in context.Diagnostics)
        {
            ImmutableDictionary<string, string?> properties = diagnostic.Properties;

            foreach (KeyValuePair<string, string?> entry in properties)
            {
                if (entry.Key == "LOCFILES")
                {
                    locFiles = entry.Value;
                }
                else
                {
                    missingKeys.Add(entry.Key, entry.Value);
                }
            }
        }

        if (locFiles == null)
        {
            return;
        }

        if (missingKeys.Count == 0)
        {
            return;
        }

        SyntaxNode? root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root == null)
        {
            return;
        }

        TextSpan diagnosticSpan = context.Diagnostics.First().Location.SourceSpan;
        SyntaxNode? declaration = root.FindToken(diagnosticSpan.Start).Parent;
        while (declaration != null && declaration.Kind() is not SyntaxKind.ClassDeclaration)
        {
            declaration = declaration.Parent;
        }
        if (declaration == null)
        {
            return;
        }

        context.RegisterCodeFix(
            CodeAction.Create(
                title: string.Format(Resources.STS001CodeFixTitle, locFiles),
                createChangedDocument: c => GeneratingMissingKeyComment(context.Document, declaration, missingKeys, c),
                equivalenceKey: nameof(Resources.STS001CodeFixTitle)
            ),
            context.Diagnostics
        );
    }

    private async Task<Document> GeneratingMissingKeyComment(Document document, SyntaxNode declaration, Dictionary<string, string?> missingKeys,
        CancellationToken cancellationToken)
    {
        StringBuilder commentBuilder = new();//"/*\n");
        
        bool first = true;
        foreach (KeyValuePair<string, string?> entry in missingKeys.ToImmutableSortedDictionary())
        {
            if (first)
            {
                first = false;
            }
            else
            {
                commentBuilder.AppendLine(",");
            }

            commentBuilder.Append($"  \"{entry.Key}\": \"{entry.Value}\"");
        }
        commentBuilder.AppendLine();//.AppendLine("*/");

        DocumentEditor editor = await DocumentEditor.CreateAsync(document, cancellationToken);
        SyntaxTrivia comment = SyntaxFactory.Comment(commentBuilder.ToString());
        
        editor.ReplaceNode(declaration, (node, generator) =>
        {
            if (node.HasLeadingTrivia)
            {
                SyntaxTriviaList trivia = node.GetLeadingTrivia().Add(comment);
                return node.WithLeadingTrivia(trivia);
            }
            else
            {
                return node.WithLeadingTrivia(comment);
            }
        });
        
        return document.WithSyntaxRoot(editor.GetChangedRoot());
    }
}