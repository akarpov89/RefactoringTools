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
    /// Rewrites iteration identifier with element access expression.
    /// </summary>
    internal class ForeachToForLoopBodyRewriter : CSharpSyntaxRewriter
    {
        private readonly ElementAccessExpressionSyntax _elementAccessExpression;
        private readonly string _iterationIdentifierName;
        private readonly ISymbol _iterationVariableSymbol;
        private readonly SemanticModel _semanticModel;

        public ForeachToForLoopBodyRewriter(
            ElementAccessExpressionSyntax elementAccessExpression,
            string iterationIdentifierName,
            ISymbol iterationVariableSymbol,
            SemanticModel semanticModel)
        {
            _elementAccessExpression = elementAccessExpression;
            _iterationIdentifierName = iterationIdentifierName;
            _iterationVariableSymbol = iterationVariableSymbol;
            _semanticModel = semanticModel;
        }

        public override SyntaxNode VisitIdentifierName(IdentifierNameSyntax node)
        {
            if (node.Identifier.Text == _iterationIdentifierName)
            {
                var identifierSymbol = _semanticModel.GetSymbolInfo(node).Symbol;

                if (identifierSymbol == _iterationVariableSymbol)
                {
                    return _elementAccessExpression;
                }
            }

            return node;
        }
    }
}
