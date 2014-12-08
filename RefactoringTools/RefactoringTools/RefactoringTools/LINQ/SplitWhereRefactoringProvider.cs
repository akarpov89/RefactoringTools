// Copyright (c) Andrew Karpov. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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
using System.Composition;

namespace RefactoringTools
{
    [ExportCodeRefactoringProvider(RefactoringId, LanguageNames.CSharp), Shared]
    internal class SplitWhereRefactoringProvider : CodeRefactoringProvider
    {
        public const string RefactoringId = nameof(SplitWhereRefactoringProvider);

        public override async Task ComputeRefactoringsAsync(CodeRefactoringContext context)
        {
            var document = context.Document;
            var cancellationToken = context.CancellationToken;
            var span = context.Span;

            var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);

            var node = root.FindNode(span);

            var statement = node as StatementSyntax;

            if (statement == null)
            {
                statement = node.TryFindParentStatement();
            }

            if (statement == null || statement.IsKind(SyntaxKind.Block))
                return;

            InvocationExpressionSyntax whereInvocation;
            SimpleLambdaExpressionSyntax filter;

            if (!LinqHelper.TryFindMethodInvocation(
                statement, 
                LinqHelper.WhereMethodName, 
                lambda => lambda.Body.IsKind(SyntaxKind.LogicalAndExpression), 
                out whereInvocation, 
                out filter))
            {
                return;
            }

            var action = CodeAction.Create(
                "Split Where filter",
                c => SplitWhereAsync(document, whereInvocation, filter, c)
            );

            context.RegisterRefactoring(action);
        }

        private async Task<Document> SplitWhereAsync(
            Document document,
            InvocationExpressionSyntax whereInvocation,
            SimpleLambdaExpressionSyntax filter,
            CancellationToken c)
        {
            var newWhereInvocation = SplitLinqWhereInvocation(whereInvocation, filter);

            var syntaxRoot = await document.GetSyntaxRootAsync(c).ConfigureAwait(false);

            syntaxRoot = syntaxRoot.ReplaceNode(whereInvocation, newWhereInvocation);

            syntaxRoot = syntaxRoot.Format();

            return document.WithSyntaxRoot(syntaxRoot);
        }

        private InvocationExpressionSyntax SplitLinqWhereInvocation(
            InvocationExpressionSyntax invocation,
            SimpleLambdaExpressionSyntax filter)
        {
            var filterParameter = filter.Parameter;
            var filterExpression = (ExpressionSyntax)filter.Body;

            var factors = FactorizeExpression(filterExpression);

            var newInvocation = MakeInvocationWithLambdaArgument(
                invocation.Expression,
                filterParameter,
                factors[0]);

            for (int i = 1; i < factors.Count; ++i)
            {
                var memberAccess = SyntaxFactory.MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    newInvocation,
                    SyntaxFactory.IdentifierName("Where"));

                newInvocation = MakeInvocationWithLambdaArgument(
                    memberAccess,
                    filterParameter,
                    factors[i]);
            }

            newInvocation = newInvocation
                .WithLeadingTrivia(invocation.GetLeadingTrivia())
                .WithTrailingTrivia(invocation.GetTrailingTrivia());

            return newInvocation;
        }

        private InvocationExpressionSyntax MakeInvocationWithLambdaArgument(
            ExpressionSyntax expression, 
            ParameterSyntax lambdaParameter, 
            ExpressionSyntax lambdaBody)
        {
            var newInvocation = SyntaxFactory.InvocationExpression(
                expression,
                SyntaxFactory.ArgumentList(
                    SyntaxFactory.SingletonSeparatedList(
                        SyntaxFactory.Argument(
                            SyntaxFactory.SimpleLambdaExpression(
                                lambdaParameter,
                                lambdaBody
                            )
                        )
                    )
                )
            );

            return newInvocation;
        }

        private List<ExpressionSyntax> FactorizeExpression(ExpressionSyntax expression)
        {
            var factors = new List<ExpressionSyntax>();

            if (!expression.IsKind(SyntaxKind.LogicalAndExpression))
            {
                if (expression.IsKind(SyntaxKind.ParenthesizedExpression))
                {
                    var parenthesizedExpression = (ParenthesizedExpressionSyntax)expression;
                    factors.Add(parenthesizedExpression.Expression);
                }
                else
                {
                    factors.Add(expression);
                }
                
                return factors;
            }

            var logicalAndExpression = (BinaryExpressionSyntax)expression;

            var leftFactors = FactorizeExpression(logicalAndExpression.Left);
            var rightFactors = FactorizeExpression(logicalAndExpression.Right);

            factors.AddRange(leftFactors);
            factors.AddRange(rightFactors);

            return factors;
        }        
    }
}
