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

            //var yy = statement.DescendantNodes().Where(x => true).Where(y => true).Where(z => true);

            InvocationExpressionSyntax outerInvocation = null;
            MemberAccessExpressionSyntax innerMemberAccess = null;
            bool isFound = false;

            var whereArguments = new Stack<ArgumentSyntax>();

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

                if (!outerInvocation.Expression.IsKind(SyntaxKind.SimpleMemberAccessExpression))
                    continue;

                if (!outerMemberAccess.Expression.IsKind(SyntaxKind.InvocationExpression))
                    continue;

                var innerInvocation = (InvocationExpressionSyntax)outerMemberAccess.Expression;

                if (innerInvocation.ArgumentList.Arguments.Count != 1)
                    continue;

                if (!innerInvocation.Expression.IsKind(SyntaxKind.SimpleMemberAccessExpression))
                    continue;

                innerMemberAccess = (MemberAccessExpressionSyntax)innerInvocation.Expression;

                if (innerMemberAccess.Name.Identifier.Text != "Where")
                    continue;

                isFound = true;

                whereArguments.Push(outerInvocation.ArgumentList.Arguments[0]);
                whereArguments.Push(innerInvocation.ArgumentList.Arguments[0]);

                break;
            }

            if (!isFound)
                return null;

            var tempMemberAccess = innerMemberAccess;

            while (true)
            {
                if (!tempMemberAccess.Expression.IsKind(SyntaxKind.InvocationExpression))
                    break;

                var innerInvocation = (InvocationExpressionSyntax)tempMemberAccess.Expression;

                if (innerInvocation.ArgumentList.Arguments.Count != 1)
                    break;

                if (!innerInvocation.Expression.IsKind(SyntaxKind.SimpleMemberAccessExpression))
                    break;

                var temp = (MemberAccessExpressionSyntax)innerInvocation.Expression;

                if (temp.Name.Identifier.Text != "Where")
                    break;

                tempMemberAccess = temp;

                whereArguments.Push(innerInvocation.ArgumentList.Arguments[0]);
            }

            var innerMostWhereAccess = tempMemberAccess;

            var tempOuterInvocation = outerInvocation;

            var whereArgumentsList = whereArguments.ToList();

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

                whereArgumentsList.Add(temp.ArgumentList.Arguments[0]);

                tempOuterInvocation = temp;
            }

            var outerMostInvocation = tempOuterInvocation;

            return null;
        }

        private void Merge(
            InvocationExpressionSyntax outerMostInvocation, 
            MemberAccessExpressionSyntax innerMostWhereAccess, 
            List<ArgumentSyntax> whereArguments)
        {

        }
    }
}
