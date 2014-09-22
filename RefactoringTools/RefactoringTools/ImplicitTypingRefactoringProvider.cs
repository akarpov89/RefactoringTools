using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeRefactorings;
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
using Microsoft.CodeAnalysis.MSBuild;

namespace CodeRefactoring1
{
    [ExportCodeRefactoringProvider(StringExtensionRefactoringProvider.RefactoringId, LanguageNames.CSharp)]
    internal class ImplicitTypingRefactoringProvider : ICodeRefactoringProvider
    {
        private static readonly Lazy<MSBuildWorkspace> LazyDefaultWorkspace = new Lazy<MSBuildWorkspace>(() => MSBuildWorkspace.Create());

        private static MSBuildWorkspace DefaultWorkspace
        {
            get { return LazyDefaultWorkspace.Value; }
        }

        public const string RefactoringId = "Type To Var Refactoring";

        public async Task<IEnumerable<CodeAction>> GetRefactoringsAsync(Document document, TextSpan span, CancellationToken cancellationToken)
        {
            var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);

            var node = root.FindNode(span);

            VariableDeclarationSyntax variableDeclaration = null;

            if (node.IsKind(SyntaxKind.VariableDeclaration))
            {
                variableDeclaration = (VariableDeclarationSyntax)node.Parent;
            }
            else if ((node.IsKind(SyntaxKind.IdentifierName) || node.IsKind(SyntaxKind.PredefinedType) || node.IsKind(SyntaxKind.GenericName) || node.IsKind(SyntaxKind.NullableType))
                && node.Parent.IsKind(SyntaxKind.VariableDeclaration))
            {
                if (node.IsKind(SyntaxKind.IdentifierName))
                {
                    var identifier = (IdentifierNameSyntax)node;

                    if (identifier.Identifier.Text == "var")
                        return null;
                }

                variableDeclaration = (VariableDeclarationSyntax)node.Parent;
            }
            else if (node.IsKind(SyntaxKind.VariableDeclarator))
            {
                variableDeclaration = (VariableDeclarationSyntax)node.Parent;
            }
            else if (
                node.IsKind(SyntaxKind.ObjectCreationExpression) &&
                node.Parent.IsKind(SyntaxKind.EqualsValueClause) &&
                node.Parent.Parent.IsKind(SyntaxKind.VariableDeclarator))
            {
                variableDeclaration = (VariableDeclarationSyntax)node.Parent.Parent.Parent;
            }
            else
            {
                variableDeclaration = TryFindParentVariableDeclaration(node);
            }

            if (variableDeclaration == null)
                return null;

            var value = variableDeclaration.Variables.FirstOrDefault()?.Initializer?.Value;
            if (value == null)
                return null;

            var semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);

            var type = semanticModel.GetTypeInfo(value).Type;

            if (type.IsAnonymousType || !type.CanBeReferencedByName)
                return null;

            var action = CodeAction.Create("Use implicit typing", c => UseImplicitTyping(document, variableDeclaration, cancellationToken));

            return new[] { action };
        }

        private static VariableDeclarationSyntax TryFindParentVariableDeclaration(SyntaxNode node)
        {
            Dictionary<VariableDeclarationSyntax, IEnumerable<Dictionary<int?, string>>> xx = new Dictionary<VariableDeclarationSyntax, IEnumerable<Dictionary<int?, string>>>();

            if (node.IsKind(SyntaxKind.IdentifierName) || node.IsKind(SyntaxKind.PredefinedType) || node.IsKind(SyntaxKind.GenericName) || node.IsKind(SyntaxKind.TypeArgumentList))
            {
                if (node.Parent.IsKind(SyntaxKind.NullableType))
                {
                    if (node.Parent.Parent.IsKind(SyntaxKind.VariableDeclaration))
                    {
                        return (VariableDeclarationSyntax)node.Parent.Parent;
                    }

                    node = node.Parent.Parent;
                }
                else if (node.Parent.IsKind(SyntaxKind.GenericName))
                {
                    node = node.Parent;
                }
                else if (node.Parent.IsKind(SyntaxKind.TypeArgumentList))
                {
                    var genericName = node.Parent.Parent;

                    node = genericName.Parent;
                }
                else if (node.Parent.IsKind(SyntaxKind.GenericName))
                {
                    node = node.Parent.Parent;
                }
                else if (
                    node.Parent.IsKind(SyntaxKind.ObjectCreationExpression) &&
                    node.Parent.Parent.IsKind(SyntaxKind.EqualsValueClause) &&
                    node.Parent.Parent.Parent.IsKind(SyntaxKind.VariableDeclarator))
                {
                    return (VariableDeclarationSyntax)node.Parent.Parent.Parent.Parent;
                }
                else
                {
                    return null;
                }

                while (!node.IsKind(SyntaxKind.VariableDeclaration))
                {
                    if (node.IsKind(SyntaxKind.TypeArgumentList))
                    {
                        var genericName = node.Parent;
                        node = genericName.Parent;
                    }
                    else if (node.IsKind(SyntaxKind.GenericName))
                    {
                        node = node.Parent;
                    }
                    else if (
                        node.Parent.IsKind(SyntaxKind.ObjectCreationExpression) &&
                        node.Parent.Parent.IsKind(SyntaxKind.EqualsValueClause) &&
                        node.Parent.Parent.Parent.IsKind(SyntaxKind.VariableDeclarator))
                    {
                        node = node.Parent.Parent.Parent.Parent;
                    }
                    else if (
                        node.IsKind(SyntaxKind.ObjectCreationExpression) &&
                        node.Parent.IsKind(SyntaxKind.EqualsValueClause) &&
                        node.Parent.Parent.IsKind(SyntaxKind.VariableDeclarator))
                    {
                        node = node.Parent.Parent.Parent;
                    }
                    else
                    {
                        return null;
                    }
                }

                return (VariableDeclarationSyntax)node;
            }
            else
            {
                return null;
            }
        }

        private async Task<Solution> UseImplicitTyping(Document document, VariableDeclarationSyntax declaration, CancellationToken cancellationToken)
        {
            var typeSyntax = SyntaxFactory.ParseTypeName("var");

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