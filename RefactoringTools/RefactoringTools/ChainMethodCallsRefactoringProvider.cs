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

            var selectedStatements = block.Statements.Where(s => s.SpanStart >= span.Start && s.SpanStart <= span.End).ToArray();

            return null;
        }
    }
}
