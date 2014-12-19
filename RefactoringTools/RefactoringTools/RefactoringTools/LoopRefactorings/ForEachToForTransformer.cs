// Copyright (c) Andrew Karpov. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Simplification;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RefactoringTools
{
    internal static class ForEachToForTransformer
    {
        public static bool TryGetAction(
            ForEachStatementSyntax forEachStatement,
            SemanticModel semanticModel,
            out Func<SyntaxNode, SyntaxNode> action)
        {
            var collectionExpression = forEachStatement.Expression;

            var collectionType = semanticModel.GetTypeInfo(collectionExpression).Type;

            string lengthMemberName;

            bool hasLengthAndIndexer = DataFlowAnalysisHelper.HasLengthAndGetIndexer(
                collectionType,
                out lengthMemberName);

            if (!hasLengthAndIndexer)
            {
                action = null;
                return false;
            }

            action = syntaxRoot =>
            {
                var forStatement = ConvertToFor(forEachStatement, semanticModel, lengthMemberName);

                var newRoot = syntaxRoot.ReplaceNode((SyntaxNode)forEachStatement, forStatement);

                return newRoot.Format();
            };

            return true;
        }

        private static ForStatementSyntax ConvertToFor(
            ForEachStatementSyntax forEachStatement,
            SemanticModel semanticModel,
            string lengthMemberName)
        {
            var collectionExpression = forEachStatement.Expression;

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

            return forStatement;
        }
    }
}
