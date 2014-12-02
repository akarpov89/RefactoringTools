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
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace RefactoringTools
{
    [ExportCodeRefactoringProvider(ChangeTypingRefactoringProvider.RefactoringId, LanguageNames.CSharp)]
    internal class TupleCreateRefactoringProvider : ICodeRefactoringProvider
    {
        public const string RefactoringId = "TupleCreateRefactoringProvider";

        public async Task<IEnumerable<CodeAction>> GetRefactoringsAsync(
            Document document, TextSpan span, CancellationToken cancellationToken)
        {
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
                    return null;
            }            

            var semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);

            var typeSymbol = semanticModel.GetSymbolInfo(objectCreationSyntax.Type).Symbol as INamedTypeSymbol;

            if (typeSymbol == null)
                return null;

            if (!typeSymbol.IsGenericType)
                return null;

            if (!typeSymbol.ToDisplayString().StartsWith("System.Tuple"))
                return null;

            var argumentsExpressions = 
                objectCreationSyntax.ArgumentList.Arguments
                .Select(argument => argument.Expression)
                .ToArray();

            if (argumentsExpressions.Any(e => e.IsKind(SyntaxKind.NullLiteralExpression)))
                return null;

            foreach (var argument in argumentsExpressions)
            {
                var typeInfo = semanticModel.GetTypeInfo(argument);

                if (typeInfo.Type != typeInfo.ConvertedType)
                    return null;
            }

            var action = CodeAction.Create(
                "Use Tuple.Create", 
                c => UseTupleCreate(document, objectCreationSyntax, c));

            return new[] { action };
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
