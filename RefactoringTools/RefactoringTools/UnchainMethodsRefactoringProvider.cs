using Microsoft.CodeAnalysis.CodeRefactorings;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.Text;
using System.Threading;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.CSharp;

namespace RefactoringTools
{
    [ExportCodeRefactoringProvider(RefactoringId, LanguageNames.CSharp)]
    internal class UnchainMethodsRefactoringProvider : ICodeRefactoringProvider
    {
        public const string RefactoringId = "UnchainMethodsRefactoringProvider";

        public async Task<IEnumerable<CodeAction>> GetRefactoringsAsync(Document document, TextSpan span, CancellationToken cancellationToken)
        {
            var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);

            var node = root.FindNode(span);

            var outerInvocation = node.TryFindOuterMostParentWithinStatement<InvocationExpressionSyntax>(SyntaxKind.InvocationExpression);

            

            if (outerInvocation == null)
                return null;

            if (!outerInvocation.Expression.IsKind(SyntaxKind.SimpleMemberAccessExpression))
                return null;

            var memberAccess = (MemberAccessExpressionSyntax)outerInvocation.Expression;

            if (!memberAccess.Expression.IsKind(SyntaxKind.InvocationExpression))
                return null;

            var innerInvocation = (InvocationExpressionSyntax)memberAccess.Expression;

            var action = CodeAction.Create("Unchain calls", c => UnchainMethodCalls(document, outerInvocation, innerInvocation, cancellationToken));

            return new[] { action };
        }

        private async Task<Solution> UnchainMethodCalls(Document document, InvocationExpressionSyntax outer, InvocationExpressionSyntax inner, CancellationToken cancellationToken)
        {
            var equalsValue = SyntaxFactory.EqualsValueClause(inner);
            var declarator = SyntaxFactory.VariableDeclarator("generated1").WithInitializer(equalsValue);
            var typeSyntax = SyntaxFactory.IdentifierName("var");
            var variableDeclaration = SyntaxFactory.VariableDeclaration(typeSyntax, new SeparatedSyntaxList<VariableDeclaratorSyntax>().Add(declarator));
            var declarationStatement = SyntaxFactory.LocalDeclarationStatement(variableDeclaration);

            var identifier = SyntaxFactory.IdentifierName("generated1");
            var outerMemberAccess = (MemberAccessExpressionSyntax)outer.Expression;
            var memberAccess = SyntaxFactory.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, identifier, outerMemberAccess.Name);
            var newOuterInvocation = SyntaxFactory.InvocationExpression(memberAccess, outer.ArgumentList);

            var parentStatement = outer.TryFindParentStatement();
            var parentBlock = outer.TryFindParentBlock();

            var statements = parentBlock.Statements;
            var index = statements.IndexOf(parentStatement);

            var syntaxRoot = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);

            syntaxRoot = syntaxRoot.ReplaceNodes(new SyntaxNode[] { outer, parentBlock }, (oldNode, newNode) =>
            {
                if (oldNode == outer)
                {
                    return newOuterInvocation;
                }
                else if (oldNode == parentBlock)
                {
                    var updatedBlock = newNode as BlockSyntax;

                    var newStatements = updatedBlock.Statements.Insert(index, declarationStatement);
                    var newBlock = SyntaxFactory.Block(newStatements);

                    return newBlock;
                }
                else
                {
                    return null;
                }
            });


            syntaxRoot = syntaxRoot.Format();

            return document.WithSyntaxRoot(syntaxRoot).Project.Solution;
        }
    }
}
