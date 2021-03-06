﻿// Copyright (c) Andrew Karpov. All rights reserved.
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
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Composition;

namespace RefactoringTools
{
    /// <summary>
    /// Provides refactoring for replacing Tuple constructor invocation with type arguments with
    /// Tuple.Create method invocation.
    /// </summary>
    [ExportCodeRefactoringProvider(ChangeTypingRefactoringProvider.RefactoringId, LanguageNames.CSharp), Shared]
    internal class TupleCreateRefactoringProvider : CodeRefactoringProvider
    {
        public const string RefactoringId = nameof(TupleCreateRefactoringProvider);

        public override async Task ComputeRefactoringsAsync(CodeRefactoringContext context)
        {
            var document = context.Document;
            var cancellationToken = context.CancellationToken;
            var span = context.Span;

            var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);

            var node = root.FindNode(span);

            ObjectCreationExpressionSyntax objectCreationSyntax;

            if (node.IsKind(SyntaxKind.ObjectCreationExpression))
            {
                objectCreationSyntax = (ObjectCreationExpressionSyntax)node;
            }
            else
            {
                objectCreationSyntax =
                    node.TryFindParentWithinStatement<ObjectCreationExpressionSyntax>(SyntaxKind.ObjectCreationExpression);

                if (objectCreationSyntax == null)
                    return;
            }

            var semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);

            var typeSymbol = semanticModel.GetSymbolInfo(objectCreationSyntax.Type).Symbol as INamedTypeSymbol;

            if (typeSymbol == null)
                return;

            if (!typeSymbol.IsGenericType)
                return;

            if (!typeSymbol.ToDisplayString().StartsWith("System.Tuple"))
                return;

            var argumentsExpressions =
                objectCreationSyntax.ArgumentList.Arguments
                .Select(argument => argument.Expression)
                .ToArray();

            if (argumentsExpressions.Any(e => e.IsKind(SyntaxKind.NullLiteralExpression)))
                return;

            foreach (var argument in argumentsExpressions)
            {
                var typeInfo = semanticModel.GetTypeInfo(argument);

                if (typeInfo.Type != typeInfo.ConvertedType)
                    return;
            }

            var action = CodeAction.Create(
                "Use Tuple.Create",
                c => UseTupleCreate(document, objectCreationSyntax, c));

            context.RegisterRefactoring(action);
        }

        private async Task<Solution> UseTupleCreate(
            Document document, 
            ObjectCreationExpressionSyntax objectCreationExpression, 
            CancellationToken cancellationToken)
        {
            var createMethodExpression = SyntaxFactory.MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression, 
                SyntaxFactory.IdentifierName("Tuple"), 
                SyntaxFactory.IdentifierName("Create"));

            var createExpression = SyntaxFactory.InvocationExpression(
                createMethodExpression, 
                objectCreationExpression.ArgumentList);

            var t = Tuple.Create(1, "str");

            var syntaxRoot = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);

            syntaxRoot = syntaxRoot.ReplaceNode<SyntaxNode, SyntaxNode>(objectCreationExpression, createExpression);

            return document.WithSyntaxRoot(syntaxRoot).Project.Solution;
        }
    }
}
