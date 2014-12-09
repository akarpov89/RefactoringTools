// Copyright (c) Andrew Karpov. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.CSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace RefactoringTools
{
    /// <summary>
    /// Substitutes identifier node with specified node.
    /// </summary>
    internal sealed class SubstituteRewriter : CSharpSyntaxRewriter
    {
        private readonly string _identifierName;
        private readonly ISymbol _identifierSymbol;
        private readonly SemanticModel _semanticModel;
        private readonly ExpressionSyntax _replacement;

        public SubstituteRewriter(
            string identifierName,
            ISymbol identifierSymbol,
            SemanticModel semanticModel,
            ExpressionSyntax replacement)
        {
            _identifierName = identifierName;
            _identifierSymbol = identifierSymbol;
            _semanticModel = semanticModel;
            _replacement = replacement;
        }

        public override SyntaxNode VisitIdentifierName(IdentifierNameSyntax node)
        {
            if (node.Identifier.Text == this._identifierName)
            {
                var currentIdentifierSymbol = this._semanticModel.GetSymbolInfo(node).Symbol;

                if (currentIdentifierSymbol == this._identifierSymbol)
                {
                    return _replacement;
                }
            }

            return node;
        }
    }
}
