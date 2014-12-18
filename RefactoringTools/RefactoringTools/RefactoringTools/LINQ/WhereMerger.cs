// Copyright (c) Andrew Karpov. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Simplification;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace RefactoringTools
{
    internal static class WhereMerger
    {
        public static bool TryGetAction(
            StatementSyntax statement,
            out Func<SyntaxNode, SemanticModel, SyntaxNode> action)
        {
            InvocationExpressionSyntax outerMostInvocation;
            MemberAccessExpressionSyntax innerMostWhereAccess;
            List<ExpressionSyntax> whereArgumentsList;

            bool isFound = LinqHelper.TryFindMethodSequence(
                statement,
                LinqHelper.WhereMethodName,
                null,
                out outerMostInvocation,
                out innerMostWhereAccess,
                out whereArgumentsList);

            if (!isFound)
            {
                action = null;
                return false;
            }

            action = (syntaxRoot, semanticModel) =>
            {
                var newInvocation = Merge(
                    outerMostInvocation,
                    innerMostWhereAccess,
                    whereArgumentsList,
                    semanticModel);

                newInvocation = newInvocation
                    .WithTriviaFrom(outerMostInvocation)
                    .WithoutAnnotations(Simplifier.Annotation);

                syntaxRoot = syntaxRoot.ReplaceNode((SyntaxNode)outerMostInvocation, newInvocation);

                return syntaxRoot.Format();
            };

            return true;
        }

        private static InvocationExpressionSyntax Merge(
            InvocationExpressionSyntax outerMostInvocation,
            MemberAccessExpressionSyntax innerMostWhereAccess,
            List<ExpressionSyntax> whereArguments,
            SemanticModel semanticModel)
        {

            var firstArgument = whereArguments[0];

            string parameterName;
            ParameterSyntax firstParameter;
            IdentifierNameSyntax firstParameterIdentifier;
            ExpressionSyntax filterExpression;

            if (firstArgument.IsKind(SyntaxKind.SimpleLambdaExpression))
            {
                var lambda = (SimpleLambdaExpressionSyntax)firstArgument;
                firstParameter = lambda.Parameter;
                parameterName = firstParameter.Identifier.Text;
                firstParameterIdentifier = SyntaxFactory.IdentifierName(firstParameter.Identifier);
                filterExpression = MakeExpressionFromLambdaBody(lambda.Body);
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

                filterExpression = ExtendedSyntaxFactory.MakeInvocation(
                    firstArgument,
                    firstParameterIdentifier);
            }


            for (int i = 1; i < whereArguments.Count; ++i)
            {
                ExpressionSyntax andOperand;

                if (whereArguments[i].IsKind(SyntaxKind.SimpleLambdaExpression))
                {
                    var currentLambda = (SimpleLambdaExpressionSyntax)whereArguments[i];
                    var currentParameter = currentLambda.Parameter;
                    var currentParameterName = currentParameter.Identifier.Text;

                    if (currentParameterName != parameterName)
                    {
                        var parameterSymbol = semanticModel.GetDeclaredSymbol(currentParameter);

                        var substituteRewriter = new SubstituteRewriter(
                            currentParameterName,
                            parameterSymbol,
                            semanticModel,
                            firstParameterIdentifier);

                        var newBody = (CSharpSyntaxNode)currentLambda.Body.Accept(substituteRewriter);

                        andOperand = MakeExpressionFromLambdaBody(newBody);
                    }
                    else
                    {
                        andOperand = MakeExpressionFromLambdaBody(currentLambda.Body);
                    }
                }
                else
                {
                    andOperand = ExtendedSyntaxFactory.MakeInvocation(
                        whereArguments[i],
                        firstParameterIdentifier);
                }

                filterExpression = SyntaxFactory.BinaryExpression(
                    SyntaxKind.LogicalAndExpression,
                    filterExpression,
                    andOperand);
            }

            var newLambda = SyntaxFactory.SimpleLambdaExpression(
                firstParameter,
                filterExpression);

            var newInvocation = ExtendedSyntaxFactory.MakeInvocation(
                innerMostWhereAccess,
                newLambda);

            return newInvocation;
        }

        private static ExpressionSyntax MakeExpressionFromLambdaBody(CSharpSyntaxNode body)
        {
            var expression = (ExpressionSyntax)body;

            if (expression.IsKind(SyntaxKind.ParenthesizedExpression)
                || expression.IsKind(SyntaxKind.LogicalNotExpression)
                || expression.IsKind(SyntaxKind.SimpleMemberAccessExpression)
                || expression.IsKind(SyntaxKind.TrueLiteralExpression)
                || expression.IsKind(SyntaxKind.FalseLiteralExpression)
                || expression.IsKind(SyntaxKind.IdentifierName)
                || expression.IsKind(SyntaxKind.InvocationExpression))
            {
                return expression;
            }

            return SyntaxFactory.ParenthesizedExpression(expression);
        }
    }
}
