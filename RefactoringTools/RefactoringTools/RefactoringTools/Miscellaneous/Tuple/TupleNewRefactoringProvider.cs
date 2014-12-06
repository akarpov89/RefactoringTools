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
using System.Composition;

namespace RefactoringTools
{
    [ExportCodeRefactoringProvider(RefactoringId, LanguageNames.CSharp), Shared]
    internal class TupleNewRefactoringProvider : CodeRefactoringProvider 
    {
        public const string RefactoringId = "TupleNewRefactoringProvider";

        public override async Task ComputeRefactoringsAsync(CodeRefactoringContext context)
        {
            var document = context.Document;
            var cancellationToken = context.CancellationToken;
            var span = context.Span;

            var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);

            var node = root.FindNode(span);

            InvocationExpressionSyntax invocationExpression;

            if (node.IsKind(SyntaxKind.InvocationExpression))
            {
                invocationExpression = (InvocationExpressionSyntax)node;
            }
            else
            {
                invocationExpression =
                    node.TryFindParentWithinStatement<InvocationExpressionSyntax>(SyntaxKind.InvocationExpression);

                if (invocationExpression == null)
                    return;
            }

            if (invocationExpression.ArgumentList.Arguments.Count == 0)
                return;

            if (!invocationExpression.Expression.IsKind(SyntaxKind.SimpleMemberAccessExpression))
                return;

            var memberAccess = (MemberAccessExpressionSyntax)invocationExpression.Expression;

            if (!memberAccess.Expression.IsKind(SyntaxKind.IdentifierName))
                return;

            var target = (IdentifierNameSyntax)memberAccess.Expression;
            if (target.Identifier.Text != "Tuple")
                return;

            if (memberAccess.Name.Identifier.Text != "Create")
                return;

            var semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);

            var typeSymbol = semanticModel.GetTypeInfo(invocationExpression).Type as INamedTypeSymbol;
            if (typeSymbol == null)
                return;

            if (!typeSymbol.ToDisplayString().StartsWith("System.Tuple"))
                return;

            var action = CodeAction.Create(
                "Use new",
                c => UseNew(document, invocationExpression, typeSymbol, c));

            context.RegisterRefactoring(action);
        }

        private async Task<Solution> UseNew(
            Document document, 
            InvocationExpressionSyntax invocationExpression, INamedTypeSymbol typeSymbol, 
            CancellationToken cancellationToken)
        {
            var semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);

            var typeName = typeSymbol.ToMinimalDisplayString(semanticModel, invocationExpression.SpanStart);

            var typeSyntax = SyntaxFactory.ParseTypeName(typeName);

            var objectCreationExpression = SyntaxFactory.ObjectCreationExpression(
                typeSyntax, 
                invocationExpression.ArgumentList, 
                null);

            objectCreationExpression = objectCreationExpression.Format();

            var syntaxRoot = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);

            syntaxRoot = syntaxRoot.ReplaceNode<SyntaxNode, SyntaxNode>(
                invocationExpression, 
                objectCreationExpression);

            return document.WithSyntaxRoot(syntaxRoot).Project.Solution;
        }
    }
}
