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
            List<SimpleLambdaExpressionSyntax> whereArgumentsList;

            bool isFound = LinqHelper.TryFindMethodSequence(
                statement,
                LinqHelper.SelectMethodName,
                lambdaArgument => lambdaArgument.Body.IsKind(SyntaxKind.InvocationExpression),
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
            List<SimpleLambdaExpressionSyntax> selectArguments,
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
            List<SimpleLambdaExpressionSyntax> selectArguments,
            SemanticModel semanticModel)
        {
            var firstProjection = selectArguments[0];

            var parameterName = firstProjection.Parameter.Identifier.Text;
            var firstProjectionParameter = firstProjection.Parameter;
            var resultInvocation = (InvocationExpressionSyntax)firstProjection.Body;

            if (parameterName == LinqHelper.GeneratedLambdaParameterName)
            {
                parameterName = NameHelper.GetLambdaParameterName(
                    outerMostInvocation.SpanStart, 
                    semanticModel);

                var parameterIdentifier = SyntaxFactory
                    .Identifier(parameterName)
                    .WithAdditionalAnnotations(RenameAnnotation.Create());

                firstProjectionParameter = SyntaxFactory.Parameter(parameterIdentifier);

                var newParameterIdentifier = SyntaxFactory.IdentifierName(parameterIdentifier);

                var renamer = new SubstituteRewriter(
                    LinqHelper.GeneratedLambdaParameterName, 
                    null, 
                    semanticModel, 
                    newParameterIdentifier);

                resultInvocation = (InvocationExpressionSyntax)resultInvocation.Accept(renamer);
            }

            for (int i = 1; i < selectArguments.Count; ++i)
            {
                var currentLambda = selectArguments[i];
                var currentParameter = currentLambda.Parameter;
                var currentParameterName = currentParameter.Identifier.Text;

                var parameterSymbol = 
                    currentParameter.Identifier.Text == LinqHelper.GeneratedLambdaParameterName 
                    ? null 
                    : semanticModel.GetDeclaredSymbol(currentParameter);

                var substituteRewriter = new SubstituteRewriter(
                    currentParameterName,
                    parameterSymbol,
                    semanticModel,
                    resultInvocation);

                resultInvocation = (InvocationExpressionSyntax)currentLambda
                    .Body
                    .Accept(substituteRewriter);
            }

            var newLambda = SyntaxFactory.SimpleLambdaExpression(
                firstProjectionParameter,
                resultInvocation);

            var newInvocation = SyntaxFactory.InvocationExpression(
                innerMostWhereAccess,
                SyntaxFactory.ArgumentList(
                    SyntaxFactory.SingletonSeparatedList(
                        SyntaxFactory.Argument(newLambda)
                    )
                )
            );

            return newInvocation;
        }
    }
}
