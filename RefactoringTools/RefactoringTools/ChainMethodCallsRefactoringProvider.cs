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
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace RefactoringTools
{
    //[ExportCodeRefactoringProvider(RefactoringId, LanguageNames.CSharp)]
    internal class ChainMethodCallsRefactoringProvider : ICodeRefactoringProvider 
    {
        public const string RefactoringId = "ChainMethodsRefactoringProvider";

        public async Task<IEnumerable<CodeAction>> GetRefactoringsAsync(Document document, TextSpan span, CancellationToken cancellationToken)
        {
            var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);

            var node = root.FindNode(span);

            if (!node.IsKind(SyntaxKind.Block))
                return null;

            var block = (BlockSyntax)node;

            var selectedStatements = block.Statements.Where(s => s.SpanStart >= span.Start && s.SpanStart <= span.End).ToList();            
            var lastStatementInBlock = block.Statements.Last();

            if (selectedStatements.Count < 2)
                return null;

            var selectionStartIndex = block.Statements.IndexOf(selectedStatements[0]);

            var currentDeclarationInfo = GetDeclarationInfo(selectedStatements[0], false);

            if (currentDeclarationInfo == null)
                return null;

            //
            // Check all selected statements but last for pattern:
            // var var0 = <smth>
            // var var1 = var0.<some method>
            //

            for (int i = 1; i < selectedStatements.Count - 1; ++i)
            {
                var nextDeclarationInfo = GetDeclarationInfo(selectedStatements[i], true);

                if (nextDeclarationInfo == null)
                    return null;

                var currentIdentifier = currentDeclarationInfo.Item1;
                var nextInfocationTarget = nextDeclarationInfo.Item2;

                if (currentIdentifier.ToString() != nextInfocationTarget.ToString())
                    return null;

                if (IsReferencedIn(currentIdentifier.ToString(), block, selectionStartIndex + i + 1))
                    return null;

                currentDeclarationInfo = nextDeclarationInfo;
            }

            //
            // The last statement could differ from LocalDeclarationStatement, so we just search for 
            // invocation on MemberAccessExpressionSyntax where Expression is last variable in chain
            //

            var lastVariableName = currentDeclarationInfo.Item1.ToString();

            var lastInvocation = selectedStatements.Last().DescendantNodes().FirstOrDefault(s =>
            {
                if (!s.IsKind(SyntaxKind.InvocationExpression))
                    return false;

                var invocation = (InvocationExpressionSyntax)s;

                if (!invocation.Expression.IsKind(SyntaxKind.SimpleMemberAccessExpression))
                    return false;                

                var memberAccess = (MemberAccessExpressionSyntax)invocation.Expression;

                if (!memberAccess.Expression.IsKind(SyntaxKind.IdentifierName))
                    return false;

                var identifier = (IdentifierNameSyntax)memberAccess.Expression;

                return identifier.ToString() == lastVariableName;
            }) 
            as InvocationExpressionSyntax;

            if (lastInvocation == null)
                return null;

            int lastVariableReferencesCount = GetReferencesCount(lastVariableName, block, selectionStartIndex + selectedStatements.Count - 1);

            if (lastVariableReferencesCount > 1)
                return null;

            var declarations = selectedStatements.Take(selectedStatements.Count - 1).ToList();

            var action = CodeAction.Create("Chain calls", c => ChainMethodCalls(document, lastInvocation, declarations, cancellationToken));

            return new[] { action };
        }

        private async Task<Solution> ChainMethodCalls(Document document, InvocationExpressionSyntax lastInvocation, List<StatementSyntax> declarations, CancellationToken cancellationToken)
        {
            var invocations = declarations                
                .Cast<LocalDeclarationStatementSyntax>()
                .Select(d => (InvocationExpressionSyntax)d.Declaration.Variables[0].Initializer.Value)
                .ToList();

            invocations.Add(lastInvocation);

            var mergedInvocation = invocations[0];

            for (int i = 1; i < invocations.Count; ++i)
            {
                var memberAccess = (MemberAccessExpressionSyntax)invocations[i].Expression;

                var newMemberAccess = SyntaxFactory.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, mergedInvocation, memberAccess.Name);

                mergedInvocation = SyntaxFactory.InvocationExpression(newMemberAccess, invocations[i].ArgumentList);
            }

            var block = (BlockSyntax)declarations[0].Parent;

            var declarationIndexes = declarations.Select(d => block.Statements.IndexOf(d)).ToArray();

            var syntaxRoot = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);

            syntaxRoot = syntaxRoot.ReplaceNodes(new SyntaxNode[] { lastInvocation, block }, (oldNode, newNode) =>
            {
                if (oldNode == lastInvocation)
                {
                    return mergedInvocation;
                }
                else if (oldNode == block)
                {
                    var oldStatements = ((BlockSyntax)newNode).Statements;

                    SyntaxList<StatementSyntax> newStatements = new SyntaxList<StatementSyntax>();
                    
                    for (int i = 0; i < oldStatements.Count; ++i)
                    {
                        if (!declarationIndexes.Contains(i))
                        {
                            newStatements = newStatements.Add(oldStatements[i]);
                        }
                    }

                    return SyntaxFactory.Block(newStatements);
                }
                else
                {
                    return null;
                }
            });

            syntaxRoot = syntaxRoot.Format();

            return document.WithSyntaxRoot(syntaxRoot).Project.Solution;
        }

        private static bool IsReferencedIn(string variableName, BlockSyntax block, int startStatementIndex)
        {
            var statements = block.Statements;

            for (int i = startStatementIndex; i < statements.Count; ++i)
            {
                bool isReferencedInCurrentStatement = GetReferences(variableName, statements[i]).Any();

                if (isReferencedInCurrentStatement)
                    return true;
            }

            return false;
        }        

        private static int GetReferencesCount(string variableName, BlockSyntax block, int startStatementIndex)
        {
            var statements = block.Statements;
            var sum = 0;

            for (int i = startStatementIndex; i < statements.Count; ++i)
            {
                sum += GetReferences(variableName, statements[i]).Count();
            }

            return sum;
        }

        private static IEnumerable<SyntaxNodeOrToken> GetReferences(string variableName, StatementSyntax statement)
        {
            return statement.DescendantNodesAndTokens().Where(x =>
            {
                if (x.IsNode && x.IsKind(SyntaxKind.IdentifierName))
                {
                    return x.ToString() == variableName;
                }
                else
                {
                    return false;
                }
            });
        }

        private static Tuple<SyntaxToken, IdentifierNameSyntax> GetDeclarationInfo(StatementSyntax statement, bool isInitializerMustBeCallOnVariable)
        {
            if (!statement.IsKind(SyntaxKind.LocalDeclarationStatement))
                return null;

            var current = (LocalDeclarationStatementSyntax)statement;

            if (current.Declaration.Variables.Count != 1)
                return null;
            if (current.Declaration.Variables[0].Initializer == null)
                return null;

            var currentIdentifier = current.Declaration.Variables[0].Identifier;
            var currentInitializer = current.Declaration.Variables[0].Initializer.Value;

            if (!currentInitializer.IsKind(SyntaxKind.InvocationExpression))
                return null;

            if (!isInitializerMustBeCallOnVariable)
            {
                return new Tuple<SyntaxToken, IdentifierNameSyntax>(currentIdentifier, null);
            }
            else
            {
                var currentInvocation = (InvocationExpressionSyntax)currentInitializer;

                if (!currentInvocation.Expression.IsKind(SyntaxKind.SimpleMemberAccessExpression))
                    return null;

                var currentInvocationMemberAccess = (MemberAccessExpressionSyntax)currentInvocation.Expression;

                if (!currentInvocationMemberAccess.Expression.IsKind(SyntaxKind.IdentifierName))
                    return null;

                var currentInvocationTarget = (IdentifierNameSyntax)currentInvocationMemberAccess.Expression;

                return new Tuple<SyntaxToken, IdentifierNameSyntax>(currentIdentifier, currentInvocationTarget);
            }
        }
    }
}
