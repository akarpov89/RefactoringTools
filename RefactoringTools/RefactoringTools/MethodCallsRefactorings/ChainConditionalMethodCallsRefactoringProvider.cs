// Copyright (c) Andrew Karpov. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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
    internal class ChainConditionalMethodCallsRefactoringProvider : ICodeRefactoringProvider 
    {
        public const string RefactoringId = "ChainConditionalMethodCallsRefactoringProvider";

        public async Task<IEnumerable<CodeAction>> GetRefactoringsAsync(
            Document document, TextSpan span, CancellationToken cancellationToken)
        {
            var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);

            var node = root.FindNode(span);

            if (!node.IsKind(SyntaxKind.Block))
                return null;

            var block = (BlockSyntax)node;

            var selectedStatements = block.Statements.Where(s => s.IsWithin(span)).ToList();
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
            // var var1 = var0.<some method> or var0?.<some method>
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
            // - invocation on MemberAccessExpressionSyntax where Expression is last variable in chain
            // or
            // - ConditionalAccessExpressionSyntax with invocation where Expression is last variable in chain
            //

            var lastVariableName = currentDeclarationInfo.Item1.ToString();

            var lastExpression = selectedStatements.Last().DescendantNodes().FirstOrDefault(s =>
            {
                IdentifierNameSyntax identifier = null;

                if (s.IsKind(SyntaxKind.InvocationExpression))
                {
                    var invocation = (InvocationExpressionSyntax)s;

                    if (!invocation.Expression.IsKind(SyntaxKind.SimpleMemberAccessExpression))
                        return false;

                    var memberAccess = (MemberAccessExpressionSyntax)invocation.Expression;

                    if (!memberAccess.Expression.IsKind(SyntaxKind.IdentifierName))
                        return false;

                    identifier = (IdentifierNameSyntax)memberAccess.Expression;
                }
                else if (s.IsKind(SyntaxKind.ConditionalAccessExpression))
                {
                    var conditionalAccess = (ConditionalAccessExpressionSyntax)s;

                    if (!conditionalAccess.WhenNotNull.IsKind(SyntaxKind.InvocationExpression))
                        return false;

                    if (!conditionalAccess.Expression.IsKind(SyntaxKind.IdentifierName))
                        return false;

                    identifier = (IdentifierNameSyntax)conditionalAccess.Expression;
                }
                else
                {
                    return false;
                }   

                return identifier.ToString() == lastVariableName;
            })
            as ExpressionSyntax;

            if (lastExpression == null)
                return null;

            int lastVariableReferencesCount = GetReferencesCount(
                lastVariableName, 
                block, 
                selectionStartIndex + selectedStatements.Count - 1);

            if (lastVariableReferencesCount > 1)
                return null;

            var declarations = selectedStatements.Take(selectedStatements.Count - 1).ToList();

            var action = CodeAction.Create(
                "Chain calls", 
                c => ChainMethodCalls(document, lastExpression, declarations, c));

            return new[] { action };
        }

        private async Task<Solution> ChainMethodCalls(
            Document document, 
            ExpressionSyntax lastExpression, List<StatementSyntax> declarations, 
            CancellationToken cancellationToken)
        {
            var expressions = declarations
                .Cast<LocalDeclarationStatementSyntax>()
                .Select(d => d.Declaration.Variables[0].Initializer.Value)
                .ToList();

            expressions.Add(lastExpression);

            var mergedExpression = expressions[0];

            for (int i = 1; i < expressions.Count; ++i)
            {
                var currentExpression = expressions[i];

                if (mergedExpression.IsKind(SyntaxKind.InvocationExpression))
                {
                    var mergedInvocation = (InvocationExpressionSyntax)mergedExpression;                    

                    if (currentExpression.IsKind(SyntaxKind.InvocationExpression))
                    {
                        var currentInvocation = (InvocationExpressionSyntax)currentExpression;

                        var memberAccess = (MemberAccessExpressionSyntax)currentInvocation.Expression;

                        var newMemberAccess = SyntaxFactory.MemberAccessExpression(
                            SyntaxKind.SimpleMemberAccessExpression, 
                            mergedExpression, 
                            memberAccess.Name);

                        mergedExpression = SyntaxFactory.InvocationExpression(
                            newMemberAccess, 
                            currentInvocation.ArgumentList);
                    }
                    else // if (currentExpression.IsKind(SyntaxKind.ConditionalAccessExpression))
                    {
                        var currentConditionalAccess = (ConditionalAccessExpressionSyntax)currentExpression;

                        mergedExpression = SyntaxFactory.ConditionalAccessExpression(
                            mergedExpression, 
                            currentConditionalAccess.WhenNotNull);
                    }
                }
                else // if (mergedExpression.IsKind(SyntaxKind.ConditionalAccessExpression))
                {
                    var mergedConditonalAccess = (ConditionalAccessExpressionSyntax)mergedExpression;

                    if (currentExpression.IsKind(SyntaxKind.InvocationExpression))
                    {
                        var currentInvocation = (InvocationExpressionSyntax)currentExpression;

                        var memberAccess = (MemberAccessExpressionSyntax)currentInvocation.Expression;

                        var simpleMemberAccess = SyntaxFactory.MemberAccessExpression(
                            SyntaxKind.SimpleMemberAccessExpression,
                            mergedConditonalAccess.WhenNotNull,
                            memberAccess.Name);

                        var newInvocation = SyntaxFactory.InvocationExpression(
                            simpleMemberAccess, 
                            currentInvocation.ArgumentList);

                        mergedExpression = SyntaxFactory.ConditionalAccessExpression(
                            mergedConditonalAccess.Expression, 
                            newInvocation);
                    }
                    else // if (currentExpression.IsKind(SyntaxKind.ConditionalAccessExpression))
                    {
                        var currentConditionalAccess = (ConditionalAccessExpressionSyntax)currentExpression;

                        mergedExpression = SyntaxFactory.ConditionalAccessExpression(
                            mergedConditonalAccess, 
                            currentConditionalAccess.WhenNotNull);
                    }
                }
            }

            var block = (BlockSyntax)declarations[0].Parent;

            var declarationIndexes = declarations.Select(d => block.Statements.IndexOf(d)).ToArray();

            var syntaxRoot = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);

            syntaxRoot = syntaxRoot.ReplaceNodes(new SyntaxNode[] { lastExpression, block }, (oldNode, newNode) =>
            {
                if (oldNode == lastExpression)
                {
                    return mergedExpression;
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

        private static Tuple<SyntaxToken, IdentifierNameSyntax> GetDeclarationInfo(
            StatementSyntax statement, bool isInitializerMustBeCallOnVariable)
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

            if (!currentInitializer.IsKind(SyntaxKind.InvocationExpression) &&
                !currentInitializer.IsKind(SyntaxKind.ConditionalAccessExpression))
            {
                return null;
            }

            if (!isInitializerMustBeCallOnVariable)
            {
                return new Tuple<SyntaxToken, IdentifierNameSyntax>(currentIdentifier, null);
            }
            else if (currentInitializer.IsKind(SyntaxKind.InvocationExpression))
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
            else // if (currentInitializer.IsKind(SyntaxKind.ConditionalAccessExpression))
            {
                var conditionalAccess = (ConditionalAccessExpressionSyntax)currentInitializer;

                if (!conditionalAccess.WhenNotNull.IsKind(SyntaxKind.InvocationExpression))
                    return null;

                if (!conditionalAccess.Expression.IsKind(SyntaxKind.IdentifierName))
                    return null;

                var currentInvocationTarget = (IdentifierNameSyntax)conditionalAccess.Expression;

                return new Tuple<SyntaxToken, IdentifierNameSyntax>(currentIdentifier, currentInvocationTarget);
            }
        }
    }
}
