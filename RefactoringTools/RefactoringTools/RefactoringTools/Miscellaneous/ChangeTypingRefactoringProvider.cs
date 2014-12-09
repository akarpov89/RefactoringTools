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
using Microsoft.CodeAnalysis.Formatting;
using System.Composition;

namespace RefactoringTools
{
    /// <summary>
    /// Provides refactoring for converting implicit typing into explicit and vice verca.
    /// </summary>
    [ExportCodeRefactoringProvider(RefactoringId, LanguageNames.CSharp), Shared]
    internal class ChangeTypingRefactoringProvider : CodeRefactoringProvider
    {
        public const string RefactoringId = nameof(ChangeTypingRefactoringProvider);

        public override async Task ComputeRefactoringsAsync(CodeRefactoringContext context)
        {
            var document = context.Document;
            var cancellationToken = context.CancellationToken;
            var span = context.Span;            

            var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);

            var node = root.FindNode(span);

            VariableDeclarationSyntax variableDeclaration = null;

            if (node.IsKind(SyntaxKind.VariableDeclaration))
            {
                variableDeclaration = (VariableDeclarationSyntax)node.Parent;
            }
            else if (node.IsKind(SyntaxKind.LocalDeclarationStatement))
            {
                var declarationStatement = (LocalDeclarationStatementSyntax)node;
                variableDeclaration = declarationStatement.Declaration;
            }
            else
            {
                variableDeclaration =
                    node.TryFindParentWithinStatement<VariableDeclarationSyntax>(SyntaxKind.VariableDeclaration);
            }

            if (variableDeclaration == null)
                return;

            var value = variableDeclaration.Variables.FirstOrDefault()?.Initializer?.Value;
            if (value == null)
                return;

            if (variableDeclaration?.Variables.Count > 1)
                return;

            if (variableDeclaration.Type.IsKind(SyntaxKind.IdentifierName) && variableDeclaration.Type.IsVar)
            {
                // To explicit

                var semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);

                var variableType = semanticModel.GetTypeInfo(value, cancellationToken);

                if (variableType.Type.IsAnonymousType)
                    return;

                var action = CodeAction.Create(
                    "Specify type explicitly",
                    c => UseExplicitTyping(document, variableDeclaration, variableType.Type, c));

                context.RegisterRefactoring(action);
            }
            else
            {
                // To implicit

                if (variableDeclaration.Parent.IsKind(SyntaxKind.LocalDeclarationStatement))
                {
                    var declarationStatement = (LocalDeclarationStatementSyntax)variableDeclaration.Parent;
                    if (declarationStatement.IsConst)
                        return;
                }

                var action = CodeAction.Create(
                    "Use var",
                    c => UseImplicitTyping(document, variableDeclaration, cancellationToken));

                context.RegisterRefactoring(action);
            }
        }

        private async Task<Solution> UseExplicitTyping(
            Document document, 
            VariableDeclarationSyntax declaration, ITypeSymbol variableType, 
            CancellationToken cancellationToken)
        {
            var semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);

            var typeName = variableType.ToMinimalDisplayString(semanticModel, declaration.SpanStart);

            var typeSyntax = SyntaxFactory.ParseTypeName(typeName);

            var newDeclaration = declaration
                .WithType(typeSyntax)
                .WithLeadingTrivia(declaration.GetLeadingTrivia())
                .WithTrailingTrivia(declaration.GetTrailingTrivia());

            var syntaxRoot = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);

            syntaxRoot = syntaxRoot.ReplaceNode(declaration, newDeclaration);

            syntaxRoot = syntaxRoot.Format();

            return document.WithSyntaxRoot(syntaxRoot).Project.Solution;
        }

        private async Task<Solution> UseImplicitTyping(
            Document document, VariableDeclarationSyntax declaration, CancellationToken cancellationToken)
        {
            var typeSyntax = SyntaxFactory.ParseTypeName("var");

            var newDeclaration = declaration
                .WithType(typeSyntax)
                .WithLeadingTrivia(declaration.GetLeadingTrivia())
                .WithTrailingTrivia(declaration.GetTrailingTrivia());

            var syntaxRoot = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);

            syntaxRoot = syntaxRoot.ReplaceNode(declaration, newDeclaration);

            syntaxRoot = syntaxRoot.Format();

            return document.WithSyntaxRoot(syntaxRoot).Project.Solution;
        }
    }
}
