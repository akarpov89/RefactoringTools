// Copyright (c) Andrew Karpov. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RefactoringTools
{
    internal static class AllAnyTransformer
    {
        public static bool TryGetAction(
            StatementSyntax statement,
            out bool isAllToAny,
            out Func<SyntaxNode, SyntaxNode> action)
        {
            isAllToAny = false;
            action = null;

            InvocationExpressionSyntax invocation;
            SimpleLambdaExpressionSyntax lambda;

            //
            // Find LINQ All/Any invocation where predicate is invertible.
            //

            int methodIndex;

            if (!LinqHelper.TryFindMethodInvocation(
                statement,
                ImmutableArray.Create(LinqHelper.AllMethodName, LinqHelper.AnyMethodName),
                IsInvertible,
                out invocation,
                out lambda,
                out methodIndex))
            {
                return false;
            }

            isAllToAny = methodIndex == 0;

            //
            // Found invocation must be last in a chain.
            //

            if (invocation.Parent.IsKind(SyntaxKind.SimpleMemberAccessExpression) 
                || invocation.Parent.IsKind(SyntaxKind.ConditionalAccessExpression))
            {
                return false;
            }

            bool isNegationRequired = !invocation.Parent.IsKind(SyntaxKind.LogicalNotExpression);

            bool allToAny = isAllToAny;

            action = syntaxRoot =>
            {
                var newInvocation = Invert(allToAny, isNegationRequired, invocation, lambda);

                SyntaxNode newRoot;

                if (isNegationRequired)
                {
                    var finalNegation = SyntaxFactory.PrefixUnaryExpression(
                        SyntaxKind.LogicalNotExpression,
                        newInvocation);

                    newRoot = syntaxRoot.ReplaceNode((SyntaxNode)invocation, finalNegation);
                }
                else
                {
                    newRoot = syntaxRoot.ReplaceNode((SyntaxNode)invocation.Parent, newInvocation);
                }

                return newRoot.Format();
            };

            return true;
        }

        public static bool IsInvertible(SimpleLambdaExpressionSyntax lambda)
        {
            switch (lambda.Body.CSharpKind())
            {
                case SyntaxKind.LogicalNotExpression:
                case SyntaxKind.EqualsExpression:
                case SyntaxKind.NotEqualsExpression:
                case SyntaxKind.GreaterThanExpression:
                case SyntaxKind.GreaterThanOrEqualExpression:
                case SyntaxKind.LessThanExpression:
                case SyntaxKind.LessThanOrEqualExpression:
                case SyntaxKind.IdentifierName:
                case SyntaxKind.SimpleMemberAccessExpression:
                case SyntaxKind.InvocationExpression:
                    return true;
                default:
                    return false;
            }
        }

        private static ExpressionSyntax Invert(
            bool isAllToAny,
            bool isNegationRequired,
            InvocationExpressionSyntax invocation,
            SimpleLambdaExpressionSyntax lambda)
        {
            SimpleLambdaExpressionSyntax invertedLambda;

            if (lambda.Body.IsKind(SyntaxKind.LogicalNotExpression))
            {
                var negation = (PrefixUnaryExpressionSyntax)lambda.Body;

                invertedLambda = SyntaxFactory.SimpleLambdaExpression(
                    lambda.Parameter,
                    negation.Operand);
            }
            else if (lambda.Body.IsKind(SyntaxKind.IdentifierName)
                     || lambda.Body.IsKind(SyntaxKind.SimpleMemberAccessExpression)
                     || lambda.Body.IsKind(SyntaxKind.InvocationExpression))
            {
                var negation = SyntaxFactory.PrefixUnaryExpression(
                    SyntaxKind.LogicalNotExpression,
                    (ExpressionSyntax)lambda.Body);

                invertedLambda = SyntaxFactory.SimpleLambdaExpression(
                    lambda.Parameter,
                    negation);
            }
            else
            {
                var operation = (BinaryExpressionSyntax)lambda.Body;

                var invertedOperator = InvertOperator(operation.OperatorToken.CSharpKind());

                var invertedOperationToken = SyntaxFactory.Token(invertedOperator);

                var invertedOperation = operation.WithOperatorToken(invertedOperationToken);

                invertedLambda = SyntaxFactory.SimpleLambdaExpression(
                    lambda.Parameter,
                    invertedOperation);
            }

            var memberAccess = (MemberAccessExpressionSyntax)invocation.Expression;

            var newName = isAllToAny ? LinqHelper.AnyMethodName : LinqHelper.AllMethodName;

            var newNameIdentifier = SyntaxFactory.IdentifierName(newName);

            var newMemberAccess = memberAccess.WithName(newNameIdentifier);

            var newInvocation = ExtendedSyntaxFactory.MakeInvocation(
                newMemberAccess,
                invertedLambda);

            return newInvocation;
        }

        private static SyntaxKind InvertOperator(SyntaxKind operationKind)
        {
            switch (operationKind)
            {
                case SyntaxKind.EqualsEqualsToken:
                    return SyntaxKind.ExclamationEqualsToken;
                case SyntaxKind.ExclamationEqualsToken:
                    return SyntaxKind.EqualsEqualsToken;
                case SyntaxKind.GreaterThanToken:
                    return SyntaxKind.LessThanEqualsToken;
                case SyntaxKind.GreaterThanEqualsToken:
                    return SyntaxKind.LessThanToken;
                case SyntaxKind.LessThanToken:
                    return SyntaxKind.GreaterThanEqualsToken;
                case SyntaxKind.LessThanEqualsToken:
                    return SyntaxKind.GreaterThanToken;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }
}
