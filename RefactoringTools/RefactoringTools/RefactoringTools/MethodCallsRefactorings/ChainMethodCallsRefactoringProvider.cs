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
using System.Composition;

namespace RefactoringTools
{
    /// <summary>
    /// Provides refactoring for chaining method invocations and removing
    /// temporary variables.
    /// </summary>
    [ExportCodeRefactoringProvider(RefactoringId, LanguageNames.CSharp), Shared]
    internal class ChainMethodCallsRefactoringProvider : CodeRefactoringProvider 
    {
        public const string RefactoringId = nameof(ChainMethodCallsRefactoringProvider);

        public override async Task ComputeRefactoringsAsync(CodeRefactoringContext context)
        {
            var document = context.Document;
            var cancellationToken = context.CancellationToken;
            var span = context.Span;

            var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);

            var node = root.FindNode(span);

            if (!node.IsKind(SyntaxKind.Block))
                return;

            var block = (BlockSyntax)node;

            var selectedStatements = block.Statements.Where(s => s.IsWithin(span)).ToList();

            if (selectedStatements.Count < 2)
                return;

            Func<SyntaxNode, SyntaxNode> action;

            if (!CallsChainer.TryGetAction(block, selectedStatements, out action))
                return;

            var codeAction = CodeAction.Create(
                "Chain calls",
                c =>
                {
                    var newRoot = action(root);

                    return Task.FromResult(document.WithSyntaxRoot(newRoot));
                });

            context.RegisterRefactoring(codeAction);
        }
    }
}
