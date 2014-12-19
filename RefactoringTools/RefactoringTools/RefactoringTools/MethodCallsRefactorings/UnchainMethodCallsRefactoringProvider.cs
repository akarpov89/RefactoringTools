// Copyright (c) Andrew Karpov. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeRefactorings;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.Text;
using System.Threading;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.CSharp;
using System.Composition;

namespace RefactoringTools
{
    /// <summary>
    /// Provides refactoring for unchaining method invocations and introducing
    /// temporary variables.
    /// </summary>
    [ExportCodeRefactoringProvider(RefactoringId, LanguageNames.CSharp), Shared]
    internal class UnchainMethodCallsRefactoringProvider : CodeRefactoringProvider 
    {
        public const string RefactoringId = nameof(UnchainMethodCallsRefactoringProvider);

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

            Func<SyntaxNode, SyntaxNode> action;

            if (!CallsUnchainer.TryGetAction(statement, out action))
                return;

            var codeAction = CodeAction.Create(
                "Unchain calls",
                c =>
                {
                    var newRoot = action(root);

                    return Task.FromResult(document.WithSyntaxRoot(newRoot));
                });

            context.RegisterRefactoring(codeAction);
        }
    }
}
