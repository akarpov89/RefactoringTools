// Copyright (c) Andrew Karpov. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeRefactorings;
using System;
using System.Collections.Generic;
using System.Composition;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RefactoringTools
{
    [ExportCodeRefactoringProvider(RefactoringId, LanguageNames.CSharp), Shared]
    internal sealed class SplitSelectRefactoringProvider : CodeRefactoringProvider
    {
        public const string RefactoringId = nameof(SplitSelectRefactoringProvider);

        public override Task ComputeRefactoringsAsync(CodeRefactoringContext context)
        {
            throw new NotImplementedException();
        }
    }
}
