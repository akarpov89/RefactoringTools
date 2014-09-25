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

namespace RefactoringTools
{
    //[ExportCodeRefactoringProvider(RefactoringId, LanguageNames.CSharp)]
    internal class ChainMethodCallsRefactoringProvider : ICodeRefactoringProvider 
    {
        public const string RefactoringId = "ChainMethodsRefactoringProvider";

        public async Task<IEnumerable<CodeAction>> GetRefactoringsAsync(Document document, TextSpan span, CancellationToken cancellationToken)
        {
            return null;            
        }
    }
}