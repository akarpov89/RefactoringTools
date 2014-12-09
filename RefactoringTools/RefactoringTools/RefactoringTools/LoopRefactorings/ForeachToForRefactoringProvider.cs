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
using Microsoft.CodeAnalysis.Simplification;
using System.Composition;

namespace RefactoringTools
{
    /// <summary>
    /// Provides refactoring for converting foreach-loop into for-loop
    /// </summary>
    [ExportCodeRefactoringProvider(RefactoringId, LanguageNames.CSharp), Shared]
    internal class ForeachToForRefactoringProvider : CodeRefactoringProvider
    {
        public const string RefactoringId = nameof(ForeachToForRefactoringProvider);
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

            if (statement == null || !statement.IsKind(SyntaxKind.ForEachStatement))
                return;

            var forEachStatement = (ForEachStatementSyntax)statement;

            var collectionExpression = forEachStatement.Expression;

            var semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);

            var collectionType = semanticModel.GetTypeInfo(collectionExpression).Type;

            string lengthMemberName;

            bool hasLengthAndIndexer = DataFlowAnalysisHelper.HasLengthAndGetIndexer(
                collectionType,
                out lengthMemberName);

            if (!hasLengthAndIndexer)
                return;

            var action = CodeAction.Create(
                "Convert to for loop",
                c => ConvertToFor(document, forEachStatement, lengthMemberName, c));

            context.RegisterRefactoring(action);
        }

        private async Task<Document> ConvertToFor(
            Document document, ForEachStatementSyntax forEachStatement, string lengthMemberName, CancellationToken c)
        {
            var collectionExpression = forEachStatement.Expression;

            var semanticModel = await document.GetSemanticModelAsync(c).ConfigureAwait(false);

            var collectionType = semanticModel.GetTypeInfo(collectionExpression).Type;

            string counterName = NameHelper.GetLoopCounterName(forEachStatement.Statement.SpanStart, semanticModel);

            var counterIdentifier = SyntaxFactory
                .IdentifierName(counterName)
                .WithAdditionalAnnotations(RenameAnnotation.Create());

            var initializer = SyntaxFactory.EqualsValueClause(
                SyntaxFactory.LiteralExpression(
                    SyntaxKind.NumericLiteralExpression,
                    SyntaxFactory.Literal(0)
                )
            );

            var declarator = SyntaxFactory.VariableDeclarator(
                SyntaxFactory.Identifier(counterName)
                             .WithAdditionalAnnotations(RenameAnnotation.Create()), 
                null, 
                initializer);

            var counterDeclaration = SyntaxFactory.VariableDeclaration(
                SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.IntKeyword)),
                SyntaxFactory.SingletonSeparatedList(declarator));

            var lengthAccess =
                SyntaxFactory.MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    collectionExpression,
                    SyntaxFactory.IdentifierName(lengthMemberName)
                );

            var condition = SyntaxFactory.BinaryExpression(
                SyntaxKind.LessThanExpression,
                counterIdentifier,
                lengthAccess);

            var counterIncrementor = SyntaxFactory.PostfixUnaryExpression(
                SyntaxKind.PostIncrementExpression, 
                counterIdentifier);

            var elementAccess =
                SyntaxFactory.ElementAccessExpression(
                    collectionExpression,
                    SyntaxFactory.BracketedArgumentList(
                        SyntaxFactory.SingletonSeparatedList(
                            SyntaxFactory.Argument(counterIdentifier)
                        )
                    )
                );

            var rewriter = new ForeachToForLoopBodyRewriter(
                elementAccess,
                forEachStatement.Identifier.Text,
                semanticModel.GetDeclaredSymbol(forEachStatement),
                semanticModel);

            var newLoopBody = (StatementSyntax)forEachStatement.Statement.Accept(rewriter);

            var forStatement = SyntaxFactory.ForStatement(
                counterDeclaration,
                SyntaxFactory.SeparatedList<ExpressionSyntax>(),
                condition,
                SyntaxFactory.SingletonSeparatedList<ExpressionSyntax>(counterIncrementor),
                newLoopBody);

            forStatement = forStatement
                .WithTriviaFrom(forEachStatement)                
                .WithAdditionalAnnotations(Simplifier.Annotation);

            var syntaxRoot = await document.GetSyntaxRootAsync(c).ConfigureAwait(false);

            syntaxRoot = syntaxRoot.ReplaceNode((SyntaxNode)forEachStatement, forStatement);

            syntaxRoot = syntaxRoot.Format();

            return document.WithSyntaxRoot(syntaxRoot);
        }
    }
}
