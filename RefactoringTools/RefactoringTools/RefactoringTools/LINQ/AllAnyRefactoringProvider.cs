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
    internal sealed class AllAnyRefactoringProvider : CodeRefactoringProvider
    {
        public const string RefactoringId = nameof(AllAnyRefactoringProvider);

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
            bool isAllToAny;

            if (!AllAnyTransformer.TryGetAction(statement, out isAllToAny, out action))
                return;

            var codeAction = CodeAction.Create(
                isAllToAny ? "Convert to Any" : "Convert to All",
                c =>
                {
                    var newRoot = action(root);

                    return Task.FromResult(document.WithSyntaxRoot(newRoot));
                }
            );

            context.RegisterRefactoring(codeAction);
        }
    }
}
