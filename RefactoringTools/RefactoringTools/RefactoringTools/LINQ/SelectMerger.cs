// Copyright (c) Andrew Karpov. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RefactoringTools
{
    internal static class SelectMerger
    {
        private static Func<ExpressionSyntax, bool> NotLambdaOrLambdaWithInvocation = e =>
            !e.IsKind(SyntaxKind.SimpleLambdaExpression)
            || ((SimpleLambdaExpressionSyntax)e).Body.IsKind(SyntaxKind.InvocationExpression);

        public static bool TryGetAction(
            StatementSyntax statement, 
            out Func<SyntaxNode, SemanticModel, SyntaxNode> action)
        {
            action = null;

            InvocationExpressionSyntax outerMostInvocation;
            MemberAccessExpressionSyntax innerMostSelectAccess;
            List<ExpressionSyntax> selectArgumentsList;

            bool isFound = LinqHelper.TryFindMethodSequence(
                statement,
                LinqHelper.SelectMethodName,
                NotLambdaOrLambdaWithInvocation,
                out outerMostInvocation,
                out innerMostSelectAccess,
                out selectArgumentsList);

            if (!isFound)
                return false;

            action = (syntaxRoot, semanticModel) =>
            {
                var newInvocation = Merge(outerMostInvocation, innerMostSelectAccess, selectArgumentsList, semanticModel);

                syntaxRoot = syntaxRoot.ReplaceNode((SyntaxNode)outerMostInvocation, newInvocation);

                return syntaxRoot.Format();
            };

            return true;
        }

        private static InvocationExpressionSyntax Merge(
            InvocationExpressionSyntax outerMostInvocation,
            MemberAccessExpressionSyntax innerMostWhereAccess,
            List<ExpressionSyntax> selectArguments,
            SemanticModel semanticModel)
        {
            var firstArgument = selectArguments[0];

            string parameterName;
            ParameterSyntax firstParameter;
            IdentifierNameSyntax firstParameterIdentifier;
            InvocationExpressionSyntax resultInvocation;

            if (firstArgument.IsKind(SyntaxKind.SimpleLambdaExpression))
            {
                var lambda = (SimpleLambdaExpressionSyntax)firstArgument;
                firstParameter = lambda.Parameter;
                parameterName = firstParameter.Identifier.Text;
                firstParameterIdentifier = SyntaxFactory.IdentifierName(firstParameter.Identifier);
                resultInvocation = (InvocationExpressionSyntax)lambda.Body;
            }
            else
            {
                parameterName = NameHelper.GetLambdaParameterName(
                    outerMostInvocation.SpanStart,
                    semanticModel);

                var parameterIdentifier = SyntaxFactory
                    .Identifier(parameterName)
                    .WithAdditionalAnnotations(RenameAnnotation.Create());

                firstParameter = SyntaxFactory.Parameter(parameterIdentifier);

                firstParameterIdentifier = SyntaxFactory.IdentifierName(parameterIdentifier);

                resultInvocation = ExtendedSyntaxFactory.MakeInvocation(
                    firstArgument,
                    firstParameterIdentifier);
            }

            for (int i = 1; i < selectArguments.Count; ++i)
            {
                if (selectArguments[i].IsKind(SyntaxKind.SimpleLambdaExpression))
                {
                    var currentLambda = (SimpleLambdaExpressionSyntax)selectArguments[i];
                    var currentParameter = currentLambda.Parameter;
                    var currentParameterName = currentParameter.Identifier.Text;

                    var parameterSymbol = semanticModel.GetDeclaredSymbol(currentParameter);

                    var substituteRewriter = new SubstituteRewriter(
                        currentParameterName,
                        parameterSymbol,
                        semanticModel,
                        resultInvocation);

                    resultInvocation = (InvocationExpressionSyntax)currentLambda
                        .Body
                        .Accept(substituteRewriter);
                }
                else
                {
                    resultInvocation = ExtendedSyntaxFactory.MakeInvocation(
                        selectArguments[i],
                        resultInvocation);
                }
            }

            var newLambda = SyntaxFactory.SimpleLambdaExpression(
                firstParameter,
                resultInvocation);

            var newInvocation = ExtendedSyntaxFactory.MakeInvocation(
                innerMostWhereAccess,
                newLambda);

            return newInvocation;
        }
    }
}
