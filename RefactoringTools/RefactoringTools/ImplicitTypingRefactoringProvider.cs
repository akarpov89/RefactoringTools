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


namespace RefactoringTools
{
    [ExportCodeRefactoringProvider(ImplicitTypingRefactoringProvider.RefactoringId, LanguageNames.CSharp)]
    internal class ImplicitTypingRefactoringProvider : ICodeRefactoringProvider
    {
        private static readonly Lazy<MSBuildWorkspace> LazyDefaultWorkspace = new Lazy<MSBuildWorkspace>(() => MSBuildWorkspace.Create());

        private static MSBuildWorkspace DefaultWorkspace
        {
            get { return LazyDefaultWorkspace.Value; }
        }

        public const string RefactoringId = "ImplicitTypingRefactoringProvider";

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

            var value = variableDeclaration.Variables.FirstOrDefault()?.Initializer?.Value;
            if (value == null)
                return null;

            if (variableDeclaration?.Variables.Count > 1)
                return null;

            if (variableDeclaration.Type.IsKind(SyntaxKind.IdentifierName))
            {
                if (variableDeclaration.Type.IsVar)
                    return null;                
            }

            if (variableDeclaration.Parent.IsKind(SyntaxKind.LocalDeclarationStatement))
            {
                var declarationStatement = (LocalDeclarationStatementSyntax)variableDeclaration.Parent;
                if (declarationStatement.IsConst)
                    return null;
            }

            var action = CodeAction.Create("Use implicit typing", c => UseImplicitTyping(document, variableDeclaration, cancellationToken));

            return new[] { action };
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