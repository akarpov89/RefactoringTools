﻿using Microsoft.CodeAnalysis.CodeRefactorings;
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
    [ExportCodeRefactoringProvider(RefactoringId, LanguageNames.CSharp)]
    internal class TupleNewRefactoringProvider : ICodeRefactoringProvider 
    {
        public const string RefactoringId = "TupleNewRefactoringProvider";

        public async Task<IEnumerable<CodeAction>> GetRefactoringsAsync(
            Document document, TextSpan span, CancellationToken cancellationToken)
        {
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
                    return null;
            }

            if (invocationExpression.ArgumentList.Arguments.Count == 0)
                return null;

            if (!invocationExpression.Expression.IsKind(SyntaxKind.SimpleMemberAccessExpression))
                return null;

            var memberAccess = (MemberAccessExpressionSyntax)invocationExpression.Expression;

            if (!memberAccess.Expression.IsKind(SyntaxKind.IdentifierName))
                return null;

            var target = (IdentifierNameSyntax)memberAccess.Expression;
            if (target.Identifier.Text != "Tuple")
                return null;

            if (memberAccess.Name.Identifier.Text != "Create")
                return null;

            var semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);

            var typeSymbol = semanticModel.GetTypeInfo(invocationExpression).Type as INamedTypeSymbol;
            if (typeSymbol == null)
                return null;

            if (!typeSymbol.ToDisplayString().StartsWith("System.Tuple"))
                return null;

            var action = CodeAction.Create(
                "Use new", 
                c => UseNew(document, invocationExpression, typeSymbol, c));

            return new[] { action };
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
