using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeRefactorings;
using Microsoft.CodeAnalysis.MSBuild;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.Text;
using System.Threading;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Formatting;

namespace RefactoringTools
{
    [ExportCodeRefactoringProvider(ExplicitTypingRefactoringProvider.RefactoringId, LanguageNames.CSharp)]
    internal class ExplicitTypingRefactoringProvider : ICodeRefactoringProvider
    {
        private static readonly Lazy<MSBuildWorkspace> LazyDefaultWorkspace = new Lazy<MSBuildWorkspace>(() => MSBuildWorkspace.Create());

        private static MSBuildWorkspace DefaultWorkspace
        {
            get { return LazyDefaultWorkspace.Value; }
        }

        public const string RefactoringId = "Explicit typing";

        public async Task<IEnumerable<CodeAction>> GetRefactoringsAsync(Document document, TextSpan span, CancellationToken cancellationToken)
        {
            var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);

            var node = root.FindNode(span);

            VariableDeclarationSyntax variableDeclaration = null;

            if (node.IsKind(SyntaxKind.VariableDeclaration))
            {
                variableDeclaration = (VariableDeclarationSyntax)node.Parent;
            }
            else if (node.IsKind(SyntaxKind.LocalDeclarationStatement))
            {
                var declarationStatement = (LocalDeclarationStatementSyntax)node;
                variableDeclaration = declarationStatement.Declaration;
            }            
            else
            {
                variableDeclaration = node.TryFindParentWithinStatement<VariableDeclarationSyntax>(SyntaxKind.VariableDeclaration);
            }

            if (variableDeclaration == null)
                return null;

            if (variableDeclaration.Type.IsKind(SyntaxKind.IdentifierName))
            {
                var typeName = (IdentifierNameSyntax)variableDeclaration.Type;
                if (typeName.Identifier.Text != "var")
                    return null;
            }
            else
            {
                return null;
            }

            var value = variableDeclaration.Variables.FirstOrDefault()?.Initializer?.Value;
            if (value == null)
                return null;            

            var semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);

            var variableType = semanticModel.GetTypeInfo(value, cancellationToken);

            if (variableType.Type.IsAnonymousType)
                return null;

            var action = CodeAction.Create("Use explicit typing", c => UseExplicitTyping(document, variableDeclaration, variableType.Type, cancellationToken));

            return new[] { action };
        }

        private async Task<Solution> UseExplicitTyping(Document document, VariableDeclarationSyntax declaration, ITypeSymbol variableType, CancellationToken cancellationToken)
        {
            var semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);

            var typeName = variableType.ToMinimalDisplayString(semanticModel, declaration.SpanStart);

            var typeSyntax = SyntaxFactory.ParseTypeName(typeName);

            var newDeclaration = declaration
                .WithType(typeSyntax)
                .WithLeadingTrivia(declaration.GetLeadingTrivia())
                .WithTrailingTrivia(declaration.GetTrailingTrivia());

            var syntaxRoot = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);

            syntaxRoot = syntaxRoot.ReplaceNode(declaration, newDeclaration);

            syntaxRoot = Formatter.Format(syntaxRoot, DefaultWorkspace);

            return document.WithSyntaxRoot(syntaxRoot).Project.Solution;
        }
    }
}
