// Copyright (c) Andrew Karpov. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeRefactorings;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Simplification;
using System;
using System.Collections.Generic;
using System.Composition;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace RefactoringTools
{    
    /// <summary>
    /// Provides refactoring for merging several LINQ Select invocations into
    /// on Select with function composition.
    /// </summary>
    [ExportCodeRefactoringProvider(RefactoringId, LanguageNames.CSharp), Shared]
    internal sealed class MergeSelectRefactoringProvider : CodeRefactoringProvider
    {
        public const string RefactoringId = nameof(MergeSelectRefactoringProvider);

        private static Func<ExpressionSyntax, bool> NotLambdaOrLambdaWithInvocation = e =>
            !e.IsKind(SyntaxKind.SimpleLambdaExpression)
            || ((SimpleLambdaExpressionSyntax)e).Body.IsKind(SyntaxKind.InvocationExpression);

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

            InvocationExpressionSyntax outerMostInvocation;
            MemberAccessExpressionSyntax innerMostWhereAccess;
            List<ExpressionSyntax> whereArgumentsList;

            bool isFound = LinqHelper.TryFindMethodSequence(
                statement,
                LinqHelper.SelectMethodName,
                NotLambdaOrLambdaWithInvocation,
                out outerMostInvocation,
                out innerMostWhereAccess,
                out whereArgumentsList);

            if (!isFound)
                return;
            
            var action = CodeAction.Create(
                "Merge Select projections",
                c => MergeSelections(
                    document,
                    outerMostInvocation,
                    innerMostWhereAccess,
                    whereArgumentsList,
                    c)
            );

            context.RegisterRefactoring(action);
        }

        private static async Task<Document> MergeSelections(
            Document document,
            InvocationExpressionSyntax outerMostInvocation,
            MemberAccessExpressionSyntax innerMostSelectAccess,
            List<ExpressionSyntax> selectArguments,
            CancellationToken c)
        {
            var semanticModel = await document.GetSemanticModelAsync(c).ConfigureAwait(false);

            var newInvocation = Merge(
                outerMostInvocation,
                innerMostSelectAccess,
                selectArguments,
                semanticModel);

            newInvocation = newInvocation
                .WithTriviaFrom(outerMostInvocation)
                .WithoutAnnotations(Simplifier.Annotation);

            var syntaxRoot = await document.GetSyntaxRootAsync(c).ConfigureAwait(false);

            syntaxRoot = syntaxRoot.ReplaceNode((SyntaxNode)outerMostInvocation, newInvocation);

            syntaxRoot = syntaxRoot.Format();

            return document.WithSyntaxRoot(syntaxRoot);
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
