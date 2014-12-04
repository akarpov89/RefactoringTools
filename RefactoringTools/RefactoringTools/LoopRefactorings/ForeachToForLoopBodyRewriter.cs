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
    internal class ForeachToForLoopBodyRewriter : CSharpSyntaxRewriter
    {
        private readonly ElementAccessExpressionSyntax elementAccessExpression;
        private readonly string iterationIdentifierName;
        private readonly ISymbol iterationVariableSymbol;
        private readonly SemanticModel semanticModel;

        public ForeachToForLoopBodyRewriter(
            ElementAccessExpressionSyntax elementAccessExpression,
            string iterationIdentifierName,
            ISymbol iterationVariableSymbol,
            SemanticModel semanticModel)
        {
            this.elementAccessExpression = elementAccessExpression;
            this.iterationIdentifierName = iterationIdentifierName;
            this.iterationVariableSymbol = iterationVariableSymbol;
            this.semanticModel = semanticModel;
        }

        public override SyntaxNode VisitIdentifierName(IdentifierNameSyntax node)
        {
            if (node.Identifier.Text == iterationIdentifierName)
            {
                var identifierSymbol = semanticModel.GetSymbolInfo(node).Symbol;

                if (identifierSymbol == iterationVariableSymbol)
                {
                    return this.elementAccessExpression;
                }
            }

            return node;
        }
    }
}
