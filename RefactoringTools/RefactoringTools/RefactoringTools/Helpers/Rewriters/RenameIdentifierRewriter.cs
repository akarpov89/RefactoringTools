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
    internal sealed class RenameIdentifierRewriter : CSharpSyntaxRewriter
    {
        private readonly string oldName;
        private readonly ISymbol identifierSymbol;
        private readonly SemanticModel semanticModel;
        private readonly string newName;

        public RenameIdentifierRewriter(
            string oldName, 
            ISymbol identifierSymbol,
            SemanticModel semanticModel,
            string newName)
        {
            this.oldName = oldName;
            this.identifierSymbol = identifierSymbol;
            this.semanticModel = semanticModel;
            this.newName = newName;
        }

        public override SyntaxNode VisitIdentifierName(IdentifierNameSyntax node)
        {
            if (node.Identifier.Text == this.oldName)
            {
                var currentIdentifierSymbol = this.semanticModel.GetSymbolInfo(node).Symbol;

                if (currentIdentifierSymbol == this.identifierSymbol)
                {
                    return SyntaxFactory.IdentifierName(newName);
                }
            }

            return node;
        }
    }
}
