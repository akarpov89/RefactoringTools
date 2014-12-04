﻿// Copyright (c) Andrew Karpov. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeRefactorings;
using Microsoft.CodeAnalysis.MSBuild;
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

namespace RefactoringTools
{
    [ExportCodeRefactoringProvider(RefactoringId, LanguageNames.CSharp)]
    internal class ChangeTypingRefactoringProvider : ICodeRefactoringProvider
    {
        public const string RefactoringId = "ChangeTypingRefactoringProvider";

        public async Task<IEnumerable<CodeAction>> GetRefactoringsAsync(
            Document document, TextSpan span, CancellationToken cancellationToken)
        {
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
                return null;

            var value = variableDeclaration.Variables.FirstOrDefault()?.Initializer?.Value;
            if (value == null)
                return null;

            if (variableDeclaration?.Variables.Count > 1)
                return null;

            if (variableDeclaration.Type.IsKind(SyntaxKind.IdentifierName) && variableDeclaration.Type.IsVar)
            {
                // To explicit

                var semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);

                var variableType = semanticModel.GetTypeInfo(value, cancellationToken);

                if (variableType.Type.IsAnonymousType)
                    return null;

                var action = CodeAction.Create(
                    "Use explicit typing", 
                    c => UseExplicitTyping(document, variableDeclaration, variableType.Type, c));

                return new[] { action };
            }
            else
            {
                // To implicit

                if (variableDeclaration.Parent.IsKind(SyntaxKind.LocalDeclarationStatement))
                {
                    var declarationStatement = (LocalDeclarationStatementSyntax)variableDeclaration.Parent;
                    if (declarationStatement.IsConst)
                        return null;
                }

                var action = CodeAction.Create(
                    "Use var", 
                    c => UseImplicitTyping(document, variableDeclaration, cancellationToken));

                return new[] { action };
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