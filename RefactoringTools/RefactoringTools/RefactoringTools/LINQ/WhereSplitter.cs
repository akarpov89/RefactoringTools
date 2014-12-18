// Copyright (c) ndrew Karpov. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RefactoringTools
{
    internal static class WhereSplitter
    {
        public static bool TryGetAction(
            StatementSyntax statement,
            out Func<SyntaxNode, SyntaxNode> action)
        {
            InvocationExpressionSyntax whereInvocation;
            SimpleLambdaExpressionSyntax filter;

            if (!LinqHelper.TryFindMethodInvocation(
                statement,
                LinqHelper.WhereMethodName,
                lambda => lambda.Body.IsKind(SyntaxKind.LogicalAndExpression),
                out whereInvocation,
                out filter))
            {
                action = null;
                return false;
            }

            action = syntaxRoot =>
            {
                var newWhereInvocation = SplitLinqWhereInvocation(whereInvocation, filter);

                syntaxRoot = syntaxRoot.ReplaceNode(whereInvocation, newWhereInvocation);

                return syntaxRoot.Format();
            };

            return true;
        }

        private static InvocationExpressionSyntax SplitLinqWhereInvocation(
            InvocationExpressionSyntax invocation,
            SimpleLambdaExpressionSyntax filter)
        {
            var filterParameter = filter.Parameter;
            var filterExpression = (ExpressionSyntax)filter.Body;

            var factors = FactorizeExpression(filterExpression);

            var newInvocation = ExtendedSyntaxFactory.MakeInvocationWithLambdaArgument(
                invocation.Expression,
                filterParameter,
                factors[0]);

            for (int i = 1; i < factors.Count; ++i)
            {
                var memberAccess = SyntaxFactory.MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    newInvocation,
                    SyntaxFactory.IdentifierName(LinqHelper.WhereMethodName));

                newInvocation = ExtendedSyntaxFactory.MakeInvocationWithLambdaArgument(
                    memberAccess,
                    filterParameter,
                    factors[i]);
            }

            return newInvocation.WithTriviaFrom(invocation);
        }

        private static List<ExpressionSyntax> FactorizeExpression(ExpressionSyntax expression)
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
