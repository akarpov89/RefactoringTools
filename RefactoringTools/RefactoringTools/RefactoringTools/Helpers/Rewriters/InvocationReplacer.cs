// Copyright (c) Andrew Karpov. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RefactoringTools
{
    /// <summary>
    /// Replaces invocation nodes with specified node.
    /// </summary>
    internal class InvocationReplacer : CSharpSyntaxRewriter
    {
        private readonly Func<InvocationExpressionSyntax, bool> _isNeedToReplace;
        private readonly SyntaxNode _replacement;

        public InvocationReplacer(
            Func<InvocationExpressionSyntax, bool> isNeedToReplace,
            SyntaxNode replacement)
        {
            _isNeedToReplace = isNeedToReplace;
            _replacement = replacement;
        }

        public override SyntaxNode VisitInvocationExpression(InvocationExpressionSyntax node)
        {
            if (_isNeedToReplace(node))
            {
                return _replacement;
            }
            else
            {
                return node;
            }
        }
    }
}
