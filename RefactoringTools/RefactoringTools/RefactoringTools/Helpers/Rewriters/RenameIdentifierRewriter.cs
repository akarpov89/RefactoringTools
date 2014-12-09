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
    /// Renames identifier.
    /// </summary>
    internal sealed class RenameIdentifierRewriter : CSharpSyntaxRewriter
    {
        private readonly string _oldName;
        private readonly ISymbol _identifierSymbol;
        private readonly SemanticModel _semanticModel;
        private readonly string _newName;

        public RenameIdentifierRewriter(
            string oldName, 
            ISymbol identifierSymbol,
            SemanticModel semanticModel,
            string newName)
        {
            _oldName = oldName;
            _identifierSymbol = identifierSymbol;
            _semanticModel = semanticModel;
            _newName = newName;
        }

        public override SyntaxNode VisitIdentifierName(IdentifierNameSyntax node)
        {
            if (node.Identifier.Text == _oldName)
            {
                var currentIdentifierSymbol = _semanticModel.GetSymbolInfo(node).Symbol;

                if (currentIdentifierSymbol == _identifierSymbol)
                {
                    return SyntaxFactory.IdentifierName(_newName);
                }
            }

            return node;
        }
    }
}
