// Copyright (c) Andrew Karpov. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeRefactorings;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
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
    /// Provides refactoring for splitting one LINQ Select invocation with function composition
    /// into several Select invocations.
    /// </summary>
    [ExportCodeRefactoringProvider(RefactoringId, LanguageNames.CSharp), Shared]
    internal sealed class SplitSelectRefactoringProvider : CodeRefactoringProvider
    {
        public const string RefactoringId = nameof(SplitSelectRefactoringProvider);

        public override async Task ComputeRefactoringsAsync(CodeRefactoringContext context)
        {
            var document = context.Document;
            var cancellationToken = context.CancellationToken;
            var span = context.Span;

            var root = await document
                .GetSyntaxRootAsync(cancellationToken)
                .ConfigureAwait(false);

            var node = root.FindNode(span);

            var statement = node as StatementSyntax;

            if (statement == null)
            {
                statement = node.TryFindParentStatement();
            }

            if (statement == null || statement.IsKind(SyntaxKind.Block))
                return;

            var semanticModel = await document
                .GetSemanticModelAsync(cancellationToken)
                .ConfigureAwait(false);

            InvocationExpressionSyntax selectInvocation;
            SimpleLambdaExpressionSyntax projection;
            Stack<InvocationExpressionSyntax> invocationStack = null;

            if (!LinqHelper.TryFindMethodInvocation(
                statement,
                LinqHelper.SelectMethodName,
                selectArgument => IsFunctionComposition(
                    selectArgument, 
                    semanticModel,
                    out invocationStack),
                out selectInvocation,
                out projection))
            {
                return;
            }

            var action = CodeAction.Create(
                "Split Select projection",
                c => SplitSelectAsync(
                    selectInvocation,
                    projection,
                    invocationStack.ToArray(),
                    document,
                    c)
            );

            context.RegisterRefactoring(action);
        }

        private static async Task<Document> SplitSelectAsync(            
            InvocationExpressionSyntax selectInvocation,
            SimpleLambdaExpressionSyntax projection,
            InvocationExpressionSyntax[] invocationStack,            
            Document document,
            CancellationToken c)
        {
            var semanticModel = await document.GetSemanticModelAsync(c).ConfigureAwait(false);

            var newSelectInvocation = SplitFunctionComposition(
                selectInvocation,
                projection,
                invocationStack,
                semanticModel);

            var syntaxRoot = await document.GetSyntaxRootAsync(c).ConfigureAwait(false);

            syntaxRoot = syntaxRoot.ReplaceNode(selectInvocation, newSelectInvocation);

            syntaxRoot = syntaxRoot.Format();

            return document.WithSyntaxRoot(syntaxRoot);
        }

        private static InvocationExpressionSyntax SplitFunctionComposition(
            InvocationExpressionSyntax selectInvocation, 
            SimpleLambdaExpressionSyntax projection,
            InvocationExpressionSyntax[] invocationStack,
            SemanticModel semanticModel)
        {
            var lambdaParameter = projection.Parameter;
            var parameterName = lambdaParameter.Identifier.Text;
            var parameterSymbol = semanticModel.GetDeclaredSymbol(lambdaParameter);

            Func<InvocationExpressionSyntax, bool> isNeedToReplaceInvocation = i => 
                DataFlowAnalysisHelper.IsIdentifierReferencedIn(
                    parameterName, 
                    parameterSymbol, 
                    semanticModel, 
                    i);

            var newSelectInvocation = ExtendedSyntaxFactory.MakeInvocationWithLambdaArgument(
                selectInvocation.Expression,
                lambdaParameter,
                invocationStack[0]);

            var invocationReplacer = new InvocationReplacer(
                isNeedToReplaceInvocation,
                SyntaxFactory.IdentifierName(parameterName));

            for (int i = 1; i < invocationStack.Length; ++i)
            {
                var innerInvocation = invocationStack[i];

                var processedInnerInvocation = ReplaceInvocationsInArguments(
                    innerInvocation,
                    invocationReplacer);

                var memberAccess = SyntaxFactory.MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    newSelectInvocation,
                    SyntaxFactory.IdentifierName(LinqHelper.SelectMethodName));

                newSelectInvocation = ExtendedSyntaxFactory.MakeInvocationWithLambdaArgument(
                    memberAccess,
                    lambdaParameter,
                    processedInnerInvocation);
            }

            return newSelectInvocation.WithTriviaFrom(selectInvocation);
        }

        private static InvocationExpressionSyntax ReplaceInvocationsInArguments(
            InvocationExpressionSyntax invocation,
            InvocationReplacer replacer)
        {
            var newArguments = new SeparatedSyntaxList<ArgumentSyntax>();

            foreach (var argument in invocation.ArgumentList.Arguments)
            {
                var newArgument = (ArgumentSyntax)argument.Accept(replacer);
                newArguments = newArguments.Add(newArgument);
            }

            return SyntaxFactory.InvocationExpression(
                invocation.Expression,
                SyntaxFactory.ArgumentList(newArguments));
        }        

        private static bool IsFunctionComposition(
            SimpleLambdaExpressionSyntax selectArgument, 
            SemanticModel semanticModel,
            out Stack<InvocationExpressionSyntax> invocationStack)
        {
            invocationStack = null;

            if (!selectArgument.Body.IsKind(SyntaxKind.InvocationExpression))
                return false;

            var outerInvocation = (InvocationExpressionSyntax)selectArgument.Body;

            bool hasInnerInvocation = false;

            foreach (var argument in outerInvocation.ArgumentList.Arguments)
            {
                if (argument.Expression.IsKind(SyntaxKind.InvocationExpression))
                {
                    hasInnerInvocation = true;
                    break;
                }
            }

            if (!hasInnerInvocation)
                return false;

            var parameterName = selectArgument.Parameter.Identifier.Text;
            var parameterSymbol = semanticModel.GetDeclaredSymbol(selectArgument.Parameter);

            var compositionChecker = new FunctionCompositionChecker(
                parameterName,
                parameterSymbol,
                semanticModel);

            return compositionChecker.IsComposition(outerInvocation, out invocationStack);
        }
    }        
}
