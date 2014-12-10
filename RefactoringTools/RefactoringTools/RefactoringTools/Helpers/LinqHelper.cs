﻿// Copyright (c) Andrew Karpov. All rights reserved.
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
    /// <summary>
    /// Contains helper methods for work with LINQ methods invocations.
    /// </summary>
    internal static class LinqHelper
    {
        #region Constants

        public const string WhereMethodName = "Where";
        public const string SelectMethodName = "Select";

        internal const string GeneratedLambdaParameterName = "____generated____lambdaArgument____";

        private static readonly ParameterSyntax GeneratedLambdaParameter =
            SyntaxFactory.Parameter(SyntaxFactory.Identifier(GeneratedLambdaParameterName));

        private static readonly IdentifierNameSyntax GeneratedIdentifierForLambda =
            SyntaxFactory.IdentifierName(SyntaxFactory.Identifier(GeneratedLambdaParameterName));

        #endregion

        #region TryFindMethodSequence

        public static bool TryFindMethodSequence(
            SyntaxNode containerNode,
            string methodName,
            Func<SimpleLambdaExpressionSyntax, bool> lambdaPredicate,
            out InvocationExpressionSyntax outerMostInvocation,
            out MemberAccessExpressionSyntax innerMostMethodAccess,
            out List<SimpleLambdaExpressionSyntax> methodArguments)
        {
            outerMostInvocation = null;
            innerMostMethodAccess = null;
            methodArguments = null;

            InvocationExpressionSyntax outerInvocation;
            MemberAccessExpressionSyntax innerMemberAccess;
            var argumentsStack = new Stack<SimpleLambdaExpressionSyntax>();

            bool isFound = TryFindDoubleCall(
                containerNode,
                methodName,
                lambdaPredicate,
                argumentsStack,
                out outerInvocation,
                out innerMemberAccess);

            if (!isFound)
                return false;

            innerMostMethodAccess = FindInnerMostMethodAccess(
                methodName,
                lambdaPredicate,
                innerMemberAccess,
                argumentsStack);

            methodArguments = argumentsStack.ToList();

            outerMostInvocation = FindOuterMostMethodInvocation(
                methodName,
                lambdaPredicate,
                outerInvocation,
                methodArguments);

            return true;
        }

        private static bool TryGetLambdaFromArgument(
            ArgumentSyntax argument, 
            out SimpleLambdaExpressionSyntax lambda)
        {
            if (argument.Expression.IsKind(SyntaxKind.SimpleLambdaExpression))
            {
                lambda = (SimpleLambdaExpressionSyntax)argument.Expression;
                return true;
            }

            if (argument.Expression.IsKind(SyntaxKind.IdentifierName)
                || argument.Expression.IsKind(SyntaxKind.SimpleMemberAccessExpression))
            {
                var invocation = SyntaxFactory.InvocationExpression(
                    argument.Expression,
                    SyntaxFactory.ArgumentList(
                        SyntaxFactory.SingletonSeparatedList(
                            SyntaxFactory.Argument(GeneratedIdentifierForLambda)
                        )
                    )
                );

                lambda = SyntaxFactory.SimpleLambdaExpression(GeneratedLambdaParameter, invocation);
                return true;
            }

            lambda = null;
            return false;
        }

        private static bool TryFindDoubleCall(
            SyntaxNode containerNode,
            string methodName,
            Func<SimpleLambdaExpressionSyntax, bool> lambdaPredicate,
            Stack<SimpleLambdaExpressionSyntax> methodArguments,
            out InvocationExpressionSyntax outerInvocation,
            out MemberAccessExpressionSyntax innerMemberAccess)
        {
            outerInvocation = null;
            innerMemberAccess = null;

            bool isFound = false;

            foreach (var x in containerNode.DescendantNodes())
            {
                if (!x.IsKind(SyntaxKind.IdentifierName)
                    || !x.Parent.IsKind(SyntaxKind.SimpleMemberAccessExpression))
                {
                    continue;
                }

                var outerMethodName = (IdentifierNameSyntax)x;

                if (outerMethodName.Identifier.Text != methodName)
                    continue;

                var outerMemberAccess = (MemberAccessExpressionSyntax)x.Parent;

                if (!outerMemberAccess.Parent.IsKind(SyntaxKind.InvocationExpression))
                    continue;

                outerInvocation = (InvocationExpressionSyntax)outerMemberAccess.Parent;

                if (outerInvocation.ArgumentList.Arguments.Count != 1)
                    continue;

                var outerArgument = outerInvocation.ArgumentList.Arguments[0];

                SimpleLambdaExpressionSyntax outerLambda;
                if (!TryGetLambdaFromArgument(outerArgument, out outerLambda))
                    continue;

                if (lambdaPredicate != null && !lambdaPredicate(outerLambda))
                    continue;

                if (!outerInvocation.Expression.IsKind(SyntaxKind.SimpleMemberAccessExpression))
                    continue;

                if (!outerMemberAccess.Expression.IsKind(SyntaxKind.InvocationExpression))
                    continue;

                var innerInvocation = (InvocationExpressionSyntax)outerMemberAccess.Expression;

                if (innerInvocation.ArgumentList.Arguments.Count != 1)
                    continue;

                var innerArgument = innerInvocation.ArgumentList.Arguments[0];

                SimpleLambdaExpressionSyntax innerLambda;
                if (!TryGetLambdaFromArgument(innerArgument, out innerLambda))
                    continue;

                if (lambdaPredicate != null && !lambdaPredicate(innerLambda))
                    continue;

                if (!innerInvocation.Expression.IsKind(SyntaxKind.SimpleMemberAccessExpression))
                    continue;

                innerMemberAccess = (MemberAccessExpressionSyntax)innerInvocation.Expression;

                if (innerMemberAccess.Name.Identifier.Text != methodName)
                    continue;

                isFound = true;

                methodArguments.Push(outerLambda);
                methodArguments.Push(innerLambda);

                break;
            }

            return isFound;
        }

        private static MemberAccessExpressionSyntax FindInnerMostMethodAccess(
            string methodName,
            Func<SimpleLambdaExpressionSyntax, bool> lambdaPredicate,
            MemberAccessExpressionSyntax memberAccess,
            Stack<SimpleLambdaExpressionSyntax> methodArguments)
        {
            var tempMemberAccess = memberAccess;

            while (true)
            {
                if (!tempMemberAccess.Expression.IsKind(SyntaxKind.InvocationExpression))
                    break;

                var innerInvocation = (InvocationExpressionSyntax)tempMemberAccess.Expression;

                if (innerInvocation.ArgumentList.Arguments.Count != 1)
                    break;

                var innerArgument = innerInvocation.ArgumentList.Arguments[0];

                SimpleLambdaExpressionSyntax innerLambda;
                if (!TryGetLambdaFromArgument(innerArgument, out innerLambda))
                    break;

                if (lambdaPredicate != null && !lambdaPredicate(innerLambda))
                    break;

                if (!innerInvocation.Expression.IsKind(SyntaxKind.SimpleMemberAccessExpression))
                    break;

                var temp = (MemberAccessExpressionSyntax)innerInvocation.Expression;

                if (temp.Name.Identifier.Text != methodName)
                    break;

                tempMemberAccess = temp;

                methodArguments.Push(innerLambda);
            }

            return tempMemberAccess;
        }

        private static InvocationExpressionSyntax FindOuterMostMethodInvocation(
            string methodName,
            Func<SimpleLambdaExpressionSyntax, bool> lambdaPredicate,
            InvocationExpressionSyntax invocation,
            List<SimpleLambdaExpressionSyntax> methodArguments)
        {
            var tempOuterInvocation = invocation;

            while (true)
            {
                if (!tempOuterInvocation.Parent.IsKind(SyntaxKind.SimpleMemberAccessExpression))
                    break;

                var memberAccess = (MemberAccessExpressionSyntax)tempOuterInvocation.Parent;

                if (memberAccess.Name.Identifier.Text != methodName)
                    break;

                if (!memberAccess.Parent.IsKind(SyntaxKind.InvocationExpression))
                    break;

                var temp = (InvocationExpressionSyntax)memberAccess.Parent;

                if (temp.ArgumentList.Arguments.Count != 1)
                    break;

                var tempArgument = temp.ArgumentList.Arguments[0];

                SimpleLambdaExpressionSyntax tempLambda;
                if (!TryGetLambdaFromArgument(tempArgument, out tempLambda))
                    break;

                if (lambdaPredicate != null && !lambdaPredicate(tempLambda))
                    break;

                methodArguments.Add(tempLambda);

                tempOuterInvocation = temp;
            }

            return tempOuterInvocation;
        }

        #endregion

        #region TryFindMethod

        public static bool TryFindMethodInvocation(
            SyntaxNode containerNode,
            string methodName,
            Func<SimpleLambdaExpressionSyntax, bool> lambdaPredicate,
            out InvocationExpressionSyntax invocation,
            out SimpleLambdaExpressionSyntax methodArgument)
        {
            invocation = null;
            methodArgument = null;
            bool isFound = false;

            foreach (var node in containerNode.DescendantNodes())
            {
                if (!node.IsKind(SyntaxKind.InvocationExpression))
                    continue;

                var currentInvocation = (InvocationExpressionSyntax)node;

                if (!currentInvocation.Expression.IsKind(SyntaxKind.SimpleMemberAccessExpression))
                    continue;

                var memberAccess = (MemberAccessExpressionSyntax)currentInvocation.Expression;

                if (memberAccess.Name.Identifier.Text != methodName)
                    continue;

                if (currentInvocation.ArgumentList.Arguments.Count != 1)
                    continue;

                var argument = currentInvocation.ArgumentList.Arguments[0];

                if (!argument.Expression.IsKind(SyntaxKind.SimpleLambdaExpression))
                    continue;

                var lambda = (SimpleLambdaExpressionSyntax)argument.Expression;

                if (!lambdaPredicate(lambda))
                    continue;

                invocation = currentInvocation;
                methodArgument = lambda;

                isFound = true;
                break;
            }

            return isFound;
        }

        #endregion

        public static InvocationExpressionSyntax MakeInvocationWithLambdaArgument(
            ExpressionSyntax expression,
            ParameterSyntax lambdaParameter,
            ExpressionSyntax lambdaBody)
        {
            ExpressionSyntax argument = null;

            if (lambdaBody.IsKind(SyntaxKind.InvocationExpression))
            {
                var invocationBody = (InvocationExpressionSyntax)lambdaBody;

                var arguments = invocationBody.ArgumentList.Arguments;

                if (arguments.Count == 1 && arguments[0].Expression.IsKind(SyntaxKind.IdentifierName))
                {
                    var invocationArgument = (IdentifierNameSyntax)arguments[0].Expression;

                    if (invocationArgument.Identifier.Text == lambdaParameter.Identifier.Text)
                    {
                        argument = invocationBody.Expression;
                    }
                }
            }

            if (argument == null)
            {
                argument = SyntaxFactory.SimpleLambdaExpression(lambdaParameter, lambdaBody);
            }

            var newInvocation = SyntaxFactory.InvocationExpression(
                expression,
                SyntaxFactory.ArgumentList(
                    SyntaxFactory.SingletonSeparatedList(
                        SyntaxFactory.Argument(argument)
                    )
                )
            );

            return newInvocation;
        }
    }
}
