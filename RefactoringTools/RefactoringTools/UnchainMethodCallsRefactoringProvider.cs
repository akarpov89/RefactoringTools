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
    internal class UnchainMethodCallsRefactoringProvider : ICodeRefactoringProvider
    {
        public const string RefactoringId = "UnchainMethodsRefactoringProvider";

        public async Task<IEnumerable<CodeAction>> GetRefactoringsAsync(Document document, TextSpan span, CancellationToken cancellationToken)
        {
            var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);

            var node = root.FindNode(span);

            var statement = node as StatementSyntax;

            InvocationExpressionSyntax outerInvocation = null;

            if (statement != null)
            {
                 outerInvocation = (InvocationExpressionSyntax) statement.DescendantNodes().FirstOrDefault(s => s.IsKind(SyntaxKind.InvocationExpression));
            }
            else
            {
                outerInvocation = node.TryFindOuterMostParentWithinStatement<InvocationExpressionSyntax>(SyntaxKind.InvocationExpression);
            }

            if (outerInvocation == null)
                return null;

            if (!outerInvocation.Expression.IsKind(SyntaxKind.SimpleMemberAccessExpression))
                return null;

            var memberAccess = (MemberAccessExpressionSyntax)outerInvocation.Expression;

            if (!memberAccess.Expression.IsKind(SyntaxKind.InvocationExpression))
                return null;

            var innerInvocation = (InvocationExpressionSyntax)memberAccess.Expression;

            var action = CodeAction.Create("Unchain calls", c => UnchainMultipleMethodCalls(document, outerInvocation, innerInvocation, cancellationToken));

            return new[] { action };
        }

        private static readonly Optional<InvocationExpressionSyntax> NotUnchainable = new Optional<InvocationExpressionSyntax>();

        private static Optional<InvocationExpressionSyntax> TryGetInnerInvocation(InvocationExpressionSyntax invocation)
        {
            if (!invocation.Expression.IsKind(SyntaxKind.SimpleMemberAccessExpression))
                return NotUnchainable;

            var memberAccess = (MemberAccessExpressionSyntax)invocation.Expression;

            if (!memberAccess.Expression.IsKind(SyntaxKind.InvocationExpression))
                return NotUnchainable;

            return new Optional<InvocationExpressionSyntax>((InvocationExpressionSyntax)memberAccess.Expression);
        }

        private static readonly SyntaxAnnotation MyRenameAnnotation = RenameAnnotation.Create();

        private InvocationExpressionSyntax Unchain(InvocationExpressionSyntax outerInvocation, List<LocalDeclarationStatementSyntax> declarations)
        {
            var optionalInner = TryGetInnerInvocation(outerInvocation);

            if (!optionalInner.HasValue)
                return outerInvocation;

            var newInner = Unchain(optionalInner.Value, declarations);

            var equalsValue = SyntaxFactory.EqualsValueClause(newInner);

            var newVariableName = "newVar" + declarations.Count.ToString();

            var declarator = SyntaxFactory.VariableDeclarator(newVariableName).WithInitializer(equalsValue);
            //declarator = declarator.ReplaceToken(declarator.Identifier, declarator.Identifier.WithAdditionalAnnotations(MyRenameAnnotation));

            var typeSyntax = SyntaxFactory.IdentifierName("var");
            var variableDeclaration = SyntaxFactory.VariableDeclaration(typeSyntax, new SeparatedSyntaxList<VariableDeclaratorSyntax>().Add(declarator));
            var declarationStatement = SyntaxFactory.LocalDeclarationStatement(variableDeclaration);

            declarations.Add(declarationStatement);

            var identifier = SyntaxFactory.IdentifierName(declarator.Identifier);
            var outerMemberAccess = (MemberAccessExpressionSyntax)outerInvocation.Expression;
            var memberAccess = SyntaxFactory.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, identifier, outerMemberAccess.Name);
            var newOuterInvocation = SyntaxFactory.InvocationExpression(memberAccess, outerInvocation.ArgumentList);

            return newOuterInvocation;
        }


        private async Task<Solution> UnchainMultipleMethodCalls(Document document, InvocationExpressionSyntax outer, InvocationExpressionSyntax inner, CancellationToken cancellationToken)
        {
            var declarations = new List<LocalDeclarationStatementSyntax>();

            var newOuterInvocation = Unchain(outer, declarations);

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

                    var newStatements = updatedBlock.Statements.InsertRange(index, declarations);

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


        private async Task<Solution> UnchainMethodCalls(Document document, InvocationExpressionSyntax outer, InvocationExpressionSyntax inner, CancellationToken cancellationToken)
        {
            var equalsValue = SyntaxFactory.EqualsValueClause(inner);

            var declarator = SyntaxFactory.VariableDeclarator("newVar").WithInitializer(equalsValue);
            declarator = declarator.ReplaceToken(declarator.Identifier, declarator.Identifier.WithAdditionalAnnotations(RenameAnnotation.Create()));

            var typeSyntax = SyntaxFactory.IdentifierName("var");
            var variableDeclaration = SyntaxFactory.VariableDeclaration(typeSyntax, new SeparatedSyntaxList<VariableDeclaratorSyntax>().Add(declarator));
            var declarationStatement = SyntaxFactory.LocalDeclarationStatement(variableDeclaration);

            var identifier = SyntaxFactory.IdentifierName(declarator.Identifier);
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
