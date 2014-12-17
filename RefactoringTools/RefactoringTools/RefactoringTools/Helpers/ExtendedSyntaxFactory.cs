// Copyright (c) Andrew Karpov. All rights reserved.
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
    /// Contains helper methods for syntax node creation.
    /// </summary>
    public static class ExtendedSyntaxFactory
    {
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

            return MakeInvocation(expression, argument);
        }

        public static InvocationExpressionSyntax MakeInvocation(
            ExpressionSyntax expression,
            ExpressionSyntax argument)
        {
            var invocation = SyntaxFactory.InvocationExpression(
                expression,
                SyntaxFactory.ArgumentList(
                    SyntaxFactory.SingletonSeparatedList(
                        SyntaxFactory.Argument(argument)
                    )
                )
            );

            return invocation;
        }
    }
}
