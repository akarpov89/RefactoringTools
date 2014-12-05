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

namespace RefactoringTools
{
    [ExportCodeRefactoringProvider(RefactoringId, LanguageNames.CSharp)]
    internal class MergeWhereRefactoringProvider : ICodeRefactoringProvider
    {
        public const string RefactoringId = nameof(MergeWhereRefactoringProvider);

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

            if (statement == null || statement.IsKind(SyntaxKind.Block))
                return null;

            InvocationExpressionSyntax outerInvocation = null;
            MemberAccessExpressionSyntax innerMemberAccess = null;
            var whereArguments = new Stack<SimpleLambdaExpressionSyntax>();

            bool isFound = TryFindLinqWhereDoubleCall(
                statement, 
                whereArguments,
                out outerInvocation, 
                out innerMemberAccess);

            if (!isFound)
                return null;

            var innerMostWhereAccess = FindInnerMostLinqWhereAccess(
                innerMemberAccess, 
                whereArguments);

            var whereArgumentsList = whereArguments.ToList();

            var outerMostInvocation = FindOuterMostLinqWhereInvocation(
                outerInvocation, 
                whereArgumentsList);

            var action = CodeAction.Create(
                "Merge Where filters",
                c => MergeWhereFilters(
                    document,
                    outerMostInvocation,
                    innerMostWhereAccess,
                    whereArgumentsList,
                    c)
            );

            return new[] { action };
        }

        private async Task<Document> MergeWhereFilters(
            Document document, 
            InvocationExpressionSyntax outerMostInvocation, 
            MemberAccessExpressionSyntax innerMostWhereAccess, 
            List<SimpleLambdaExpressionSyntax> whereArguments,
            CancellationToken c)
        {
            var semanticModel = await document.GetSemanticModelAsync(c).ConfigureAwait(false);

            var newInvocation = Merge(
                outerMostInvocation, 
                innerMostWhereAccess, 
                whereArguments, 
                semanticModel);

            newInvocation = newInvocation
                .WithLeadingTrivia(outerMostInvocation.GetLeadingTrivia())
                .WithTrailingTrivia(outerMostInvocation.GetTrailingTrivia())
                .WithoutAnnotations(Simplifier.Annotation);

            var syntaxRoot = await document.GetSyntaxRootAsync(c).ConfigureAwait(false);

            syntaxRoot = syntaxRoot.ReplaceNode((SyntaxNode)outerMostInvocation, newInvocation);

            syntaxRoot = syntaxRoot.Format();

            return document.WithSyntaxRoot(syntaxRoot);
        }

        private InvocationExpressionSyntax Merge(
            InvocationExpressionSyntax outerMostInvocation, 
            MemberAccessExpressionSyntax innerMostWhereAccess, 
            List<SimpleLambdaExpressionSyntax> whereArguments,
            SemanticModel semanticModel)
        {
            var firstFilter = whereArguments[0];

            var parameterName = firstFilter.Parameter.Identifier.Text;

            ExpressionSyntax filterExpression = MakeExpressionFromLambdaBody(firstFilter.Body);

            for (int i = 1; i < whereArguments.Count; ++i)
            {
                var currentLambda = whereArguments[i];
                var currentParameter = currentLambda.Parameter;
                var currentParameterName = currentParameter.Identifier.Text;

                ExpressionSyntax andOperand;

                if (currentParameterName != parameterName)
                {
                    var parameterSymbol = semanticModel.GetDeclaredSymbol(currentParameter);

                    var renameRewriter = new RenameIdentifierRewriter(
                        currentParameterName,
                        parameterSymbol,
                        semanticModel,
                        parameterName);

                    var newBody = (CSharpSyntaxNode)currentLambda.Body.Accept(renameRewriter);

                    andOperand = MakeExpressionFromLambdaBody(newBody);
                }
                else
                {
                    andOperand = MakeExpressionFromLambdaBody(currentLambda.Body);
                }

                filterExpression = SyntaxFactory.BinaryExpression(
                    SyntaxKind.LogicalAndExpression, 
                    filterExpression, 
                    andOperand);
            }

            var newLambda = SyntaxFactory.SimpleLambdaExpression(
                firstFilter.Parameter, 
                filterExpression);

            var newInvocation = SyntaxFactory.InvocationExpression(
                innerMostWhereAccess,
                SyntaxFactory.ArgumentList(
                    SyntaxFactory.SingletonSeparatedList(
                        SyntaxFactory.Argument(newLambda)
                    )
                )
            );

            return newInvocation;
        }

        private ExpressionSyntax MakeExpressionFromLambdaBody(CSharpSyntaxNode body)
        {
            var expression = (ExpressionSyntax)body;

            if (expression.IsKind(SyntaxKind.ParenthesizedExpression) 
                || expression.IsKind(SyntaxKind.LogicalNotExpression)
                || expression.IsKind(SyntaxKind.SimpleMemberAccessExpression)
                || expression.IsKind(SyntaxKind.TrueLiteralExpression)
                || expression.IsKind(SyntaxKind.FalseLiteralExpression))
            {
                return expression;
            }

            return SyntaxFactory.ParenthesizedExpression(expression);
        }

        private bool TryFindLinqWhereDoubleCall(
            StatementSyntax statement,
            Stack<SimpleLambdaExpressionSyntax> whereArguments,
            out InvocationExpressionSyntax outerInvocation, 
            out MemberAccessExpressionSyntax innerMemberAccess)
        {
            outerInvocation = null;
            innerMemberAccess = null;

            bool isFound = false;            

            foreach (var x in statement.DescendantNodes())
            {
                if (!x.IsKind(SyntaxKind.IdentifierName)
                    || !x.Parent.IsKind(SyntaxKind.SimpleMemberAccessExpression))
                {
                    continue;
                }

                var outerMethodName = (IdentifierNameSyntax)x;

                if (outerMethodName.Identifier.Text != "Where")
                    continue;

                var outerMemberAccess = (MemberAccessExpressionSyntax)x.Parent;

                if (!outerMemberAccess.Parent.IsKind(SyntaxKind.InvocationExpression))
                    continue;

                outerInvocation = (InvocationExpressionSyntax)outerMemberAccess.Parent;

                if (outerInvocation.ArgumentList.Arguments.Count != 1)
                    continue;

                var outerArgument = outerInvocation.ArgumentList.Arguments[0].Expression;

                if (!outerArgument.IsKind(SyntaxKind.SimpleLambdaExpression))
                    continue;

                if (!outerInvocation.Expression.IsKind(SyntaxKind.SimpleMemberAccessExpression))
                    continue;

                if (!outerMemberAccess.Expression.IsKind(SyntaxKind.InvocationExpression))
                    continue;

                var innerInvocation = (InvocationExpressionSyntax)outerMemberAccess.Expression;

                if (innerInvocation.ArgumentList.Arguments.Count != 1)
                    continue;

                var innerArgument = innerInvocation.ArgumentList.Arguments[0].Expression;

                if (!innerArgument.IsKind(SyntaxKind.SimpleLambdaExpression))
                    continue;

                if (!innerInvocation.Expression.IsKind(SyntaxKind.SimpleMemberAccessExpression))
                    continue;

                innerMemberAccess = (MemberAccessExpressionSyntax)innerInvocation.Expression;

                if (innerMemberAccess.Name.Identifier.Text != "Where")
                    continue;

                isFound = true;

                whereArguments.Push((SimpleLambdaExpressionSyntax)outerArgument);
                whereArguments.Push((SimpleLambdaExpressionSyntax)innerArgument);

                break;
            }

            return isFound;
        }

        private MemberAccessExpressionSyntax FindInnerMostLinqWhereAccess(
            MemberAccessExpressionSyntax memberAccess,
            Stack<SimpleLambdaExpressionSyntax> whereArguments)
        {
            var tempMemberAccess = memberAccess;

            while (true)
            {
                if (!tempMemberAccess.Expression.IsKind(SyntaxKind.InvocationExpression))
                    break;

                var innerInvocation = (InvocationExpressionSyntax)tempMemberAccess.Expression;

                if (innerInvocation.ArgumentList.Arguments.Count != 1)
                    break;

                var innerArgument = innerInvocation.ArgumentList.Arguments[0].Expression;

                if (!innerArgument.IsKind(SyntaxKind.SimpleLambdaExpression))
                    break;

                if (!innerInvocation.Expression.IsKind(SyntaxKind.SimpleMemberAccessExpression))
                    break;

                var temp = (MemberAccessExpressionSyntax)innerInvocation.Expression;

                if (temp.Name.Identifier.Text != "Where")
                    break;

                tempMemberAccess = temp;

                whereArguments.Push((SimpleLambdaExpressionSyntax)innerArgument);
            }

            return tempMemberAccess;
        }

        private InvocationExpressionSyntax FindOuterMostLinqWhereInvocation(
            InvocationExpressionSyntax invocation,
            List<SimpleLambdaExpressionSyntax> whereArguments)
        {
            var tempOuterInvocation = invocation;

            while (true)
            {
                if (!tempOuterInvocation.Parent.IsKind(SyntaxKind.SimpleMemberAccessExpression))
                    break;

                var memberAccess = (MemberAccessExpressionSyntax)tempOuterInvocation.Parent;

                if (memberAccess.Name.Identifier.Text != "Where")
                    break;

                if (!memberAccess.Parent.IsKind(SyntaxKind.InvocationExpression))
                    break;

                var temp = (InvocationExpressionSyntax)memberAccess.Parent;

                if (temp.ArgumentList.Arguments.Count != 1)
                    break;

                var tempArgument = temp.ArgumentList.Arguments[0].Expression;

                if (!tempArgument.IsKind(SyntaxKind.SimpleLambdaExpression))
                    break;

                whereArguments.Add((SimpleLambdaExpressionSyntax)tempArgument);

                tempOuterInvocation = temp;
            }

            return tempOuterInvocation;
        }
    }
}
