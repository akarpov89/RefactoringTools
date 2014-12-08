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
using System.Threading.Tasks;

namespace RefactoringTools
{
    [ExportCodeRefactoringProvider(RefactoringId, LanguageNames.CSharp), Shared]
    internal sealed class SplitSelectRefactoringProvider : CodeRefactoringProvider
    {
        public const string RefactoringId = nameof(SplitSelectRefactoringProvider);

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

            InvocationExpressionSyntax selectInvocation;
            SimpleLambdaExpressionSyntax projection;

            if (!LinqHelper.TryFindMethodInvocation(
                statement,
                LinqHelper.SelectMethodName,
                IsFunctionComposition,
                out selectInvocation,
                out projection))
            {
                return;
            }

            /*
            var action = CodeAction.Create(
                "Split Where filter",
                c => SplitWhereAsync(document, whereInvocation, filter, c)
            );

            context.RegisterRefactoring(action);*/
        }

        //private static InvocationExpressionSyntax SplitFunctionCompoition(
        //    InvocationExpressionSyntax selectInvocation, 
        //    SimpleLambdaExpressionSyntax projection)
        //{

        //}

        private bool IsFunctionComposition(SimpleLambdaExpressionSyntax selectArgument)
        {
            if (!selectArgument.Body.IsKind(SyntaxKind.InvocationExpression))
                return false;

            var outerInvocation = (InvocationExpressionSyntax)selectArgument.Body;

            SyntaxNode innerInvocationNode = null;

            foreach (var argument in outerInvocation.ArgumentList.Arguments)
            {
                if (argument.Expression.IsKind(SyntaxKind.InvocationExpression))
                {
                    if (innerInvocationNode == null)
                    {
                        innerInvocationNode = argument.Expression;
                    }
                    else if (!argument.Expression.IsEquivalentTo(innerInvocationNode))
                    {
                        return false;
                    }
                }
            }

            return innerInvocationNode != null;
        }
    }
}
