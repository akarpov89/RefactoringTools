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
    [ExportCodeRefactoringProvider(RefactoringId, LanguageNames.CSharp)]
    internal class ChainMethodCallsRefactoringProvider : ICodeRefactoringProvider 
    {
        public const string RefactoringId = "ChainMethodsRefactoringProvider";

        public async Task<IEnumerable<CodeAction>> GetRefactoringsAsync(Document document, TextSpan span, CancellationToken cancellationToken)
        {
            var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);

            var node = root.FindNode(span);

            if (!node.IsKind(SyntaxKind.Block))
                return null;

            var block = (BlockSyntax)node;

            var selectedStatements = block.Statements.Where(s => s.SpanStart >= span.Start && s.SpanStart <= span.End).ToList();

            if (selectedStatements.Count == 0)
                return null;

            for (int i = 0; i < selectedStatements.Count - 2; ++i)
            {
                if (!selectedStatements[i].IsKind(SyntaxKind.LocalDeclarationStatement))
                    return null;

                if (!selectedStatements[i + 1].IsKind(SyntaxKind.LocalDeclarationStatement))
                    return null;

                var current = (LocalDeclarationStatementSyntax)selectedStatements[i];
                var next = (LocalDeclarationStatementSyntax)selectedStatements[i + 1];

                if (current.Declaration.Variables.Count != 1)
                    return null;
                if (current.Declaration.Variables[0].Initializer == null)
                    return null;

                if (next.Declaration.Variables.Count != 1)
                    return null;
                if (next.Declaration.Variables[0].Initializer == null)
                    return null;

                var currentIdentifier = current.Declaration.Variables[0].Identifier;
                var currentInitializer = current.Declaration.Variables[0].Initializer.Value;
                var nextInitializer = next.Declaration.Variables[0].Initializer.Value;

                if (!currentInitializer.IsKind(SyntaxKind.InvocationExpression))
                    return null;

                if (!nextInitializer.IsKind(SyntaxKind.InvocationExpression))
                    return null;

                var nextInvocation = (InvocationExpressionSyntax)nextInitializer;

                if (!nextInvocation.Expression.IsKind(SyntaxKind.SimpleMemberAccessExpression))
                    return null;

                var nextInvocationMemberAccess = (MemberAccessExpressionSyntax)nextInvocation.Expression;

                if (!nextInvocationMemberAccess.Expression.IsKind(SyntaxKind.IdentifierName))
                    return null;

                var nextInvocationTarget = (IdentifierNameSyntax)nextInvocationMemberAccess.Expression;

                if (nextInvocationTarget.Identifier.ToString() != currentIdentifier.ToString())
                    return null;
            }

            return null;
        }
    }
}
