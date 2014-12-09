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
using Microsoft.CodeAnalysis.Simplification;
using System.Composition;

namespace RefactoringTools
{
    /// <summary>
    /// Provides refactoring for merging several LINQ Where invocations
    /// into one Where with conjunction of predicates.
    /// </summary>
    [ExportCodeRefactoringProvider(RefactoringId, LanguageNames.CSharp), Shared]
    internal class MergeWhereRefactoringProvider : CodeRefactoringProvider
    {
        public const string RefactoringId = nameof(MergeWhereRefactoringProvider);

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
                LinqHelper.WhereMethodName,
                null,
                out outerMostInvocation,
                out innerMostWhereAccess,
                out whereArgumentsList);

            if (!isFound)
                return;

            var action = CodeAction.Create(
                "Merge Where filters",
                c => MergeWhereFilters(
                    document,
                    outerMostInvocation,
                    innerMostWhereAccess,
                    whereArgumentsList,
                    c)
            );

            context.RegisterRefactoring(action);
        }

        private async Task<Document> MergeWhereFilters(
            Document document, 
            InvocationExpressionSyntax outerMostInvocation, 
            MemberAccessExpressionSyntax innerMostWhereAccess, 
            List<SimpleLambdaExpressionSyntax> whereArguments,
            CancellationToken c)
        {
            var semanticModel = await document.GetSemanticModelAsync(c).ConfigureAwait(false);

            var newInvocation = Merge(
                outerMostInvocation, 
                innerMostWhereAccess, 
                whereArguments, 
                semanticModel);

            newInvocation = newInvocation
                .WithTriviaFrom(outerMostInvocation)                
                .WithoutAnnotations(Simplifier.Annotation);

            var syntaxRoot = await document.GetSyntaxRootAsync(c).ConfigureAwait(false);

            syntaxRoot = syntaxRoot.ReplaceNode((SyntaxNode)outerMostInvocation, newInvocation);

            syntaxRoot = syntaxRoot.Format();

            return document.WithSyntaxRoot(syntaxRoot);
        }

        private InvocationExpressionSyntax Merge(
            InvocationExpressionSyntax outerMostInvocation, 
            MemberAccessExpressionSyntax innerMostWhereAccess, 
            List<SimpleLambdaExpressionSyntax> whereArguments,
            SemanticModel semanticModel)
        {
            var firstFilter = whereArguments[0];

            var parameterName = firstFilter.Parameter.Identifier.Text;

            ExpressionSyntax filterExpression = MakeExpressionFromLambdaBody(firstFilter.Body);

            for (int i = 1; i < whereArguments.Count; ++i)
            {
                var currentLambda = whereArguments[i];
                var currentParameter = currentLambda.Parameter;
                var currentParameterName = currentParameter.Identifier.Text;

                ExpressionSyntax andOperand;

                if (currentParameterName != parameterName)
                {
                    var parameterSymbol = semanticModel.GetDeclaredSymbol(currentParameter);

                    var renameRewriter = new RenameIdentifierRewriter(
                        currentParameterName,
                        parameterSymbol,
                        semanticModel,
                        parameterName);

                    var newBody = (CSharpSyntaxNode)currentLambda.Body.Accept(renameRewriter);

                    andOperand = MakeExpressionFromLambdaBody(newBody);
                }
                else
                {
                    andOperand = MakeExpressionFromLambdaBody(currentLambda.Body);
                }

                filterExpression = SyntaxFactory.BinaryExpression(
                    SyntaxKind.LogicalAndExpression, 
                    filterExpression, 
                    andOperand);
            }

            var newLambda = SyntaxFactory.SimpleLambdaExpression(
                firstFilter.Parameter, 
                filterExpression);

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

        private ExpressionSyntax MakeExpressionFromLambdaBody(CSharpSyntaxNode body)
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
