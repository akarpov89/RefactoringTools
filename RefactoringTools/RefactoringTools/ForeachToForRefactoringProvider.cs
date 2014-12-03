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

namespace RefactoringTools
{
    [ExportCodeRefactoringProvider(RefactoringId, LanguageNames.CSharp)]
    internal class ForeachToForRefactoringProvider : ICodeRefactoringProvider
    {
        public const string RefactoringId = nameof(ForeachToForRefactoringProvider);

        public async Task<IEnumerable<CodeAction>> GetRefactoringsAsync(
            Document document, TextSpan span, CancellationToken cancellationToken)
        {
            var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);

            var node = root.FindNode(span);

            var statement = node as StatementSyntax;

            if (statement == null)
            {
                statement = node.TryFindParentStatement();
            }

            if (statement == null || !statement.IsKind(SyntaxKind.ForEachStatement))
                return null;

            var forEachStatement = (ForEachStatementSyntax)statement;


            var collectionExpression = forEachStatement.Expression;

            var semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);

            var collectionType = semanticModel.GetTypeInfo(collectionExpression).Type;

            

            return null;
        }

        private async Task<Document> ConvertToFor(
            Document document, ForEachStatementSyntax forEachStatement, CancellationToken c)
        {
            var collectionExpression = forEachStatement.Expression;

            var semanticModel = await document.GetSemanticModelAsync(c).ConfigureAwait(false);

            var collectionType = semanticModel.GetTypeInfo(collectionExpression).Type;

            string lengthMemberName;

            bool hasLengthAndIndexer = DataFlowAnalysisHelper.HasLengthAndGetIndexer(
                collectionType, 
                out lengthMemberName);

            const string counterName = "COUNTER";

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

            ExpressionSyntax newCollectionExpression;

            if (hasLengthAndIndexer)
            {
                newCollectionExpression = collectionExpression;
            }
            else
            {
                newCollectionExpression =
                    SyntaxFactory.InvocationExpression(
                        SyntaxFactory.MemberAccessExpression(
                            SyntaxKind.SimpleMemberAccessExpression,
                            collectionExpression,
                            SyntaxFactory.IdentifierName("ToArray")
                        ),
                        null
                    );

                lengthMemberName = "Length";
            }

            var lengthAccess =
                SyntaxFactory.MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    newCollectionExpression,
                    SyntaxFactory.IdentifierName(lengthMemberName)
                );

            var condition = SyntaxFactory.BinaryExpression(
                SyntaxKind.LessThanExpression,
                counterIdentifier,
                lengthAccess);

            //
            // TODO Create ForeachToForLoopBodyRewriter
            //

            return null;
        }
    }
}
