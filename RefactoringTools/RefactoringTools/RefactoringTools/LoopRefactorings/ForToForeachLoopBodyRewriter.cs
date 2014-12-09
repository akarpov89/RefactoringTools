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
    /// Rewrites element access expression with iteration identifier.
    /// </summary>
    internal class ForToForeachLoopBodyRewriter : CSharpSyntaxRewriter
    {
        private readonly IdentifierNameSyntax _iterationIdentifier;
        private readonly string _collectionPartName;
        private readonly ISymbol _collectionSymbol;
        private readonly SemanticModel _semanticModel;

        public ForToForeachLoopBodyRewriter(
            IdentifierNameSyntax iterationIdentifier,
            string collectionPartName,
            ISymbol collectionSymbol,
            SemanticModel semanticModel)
        {
            _iterationIdentifier = iterationIdentifier;
            _collectionPartName = collectionPartName;
            _collectionSymbol = collectionSymbol;
            _semanticModel = semanticModel;
        }

        public override SyntaxNode VisitElementAccessExpression(ElementAccessExpressionSyntax node)
        {
            if (node.Expression.IsKind(SyntaxKind.IdentifierName))
            {
                var identifierNode = (IdentifierNameSyntax)node.Expression;

                if (identifierNode.Identifier.Text == _collectionPartName)
                {
                    // 
                    // If identifier name equals to collection part name 
                    // we check whether are they the same symbols.
                    //

                    var nodeSymbol = _semanticModel.GetSymbolInfo(identifierNode).Symbol;

                    if (_collectionSymbol == nodeSymbol)
                    {
                        return _iterationIdentifier;
                    }
                }
            }
            else if (node.Expression.IsKind(SyntaxKind.SimpleMemberAccessExpression))
            {
                var memberAccessNode = (MemberAccessExpressionSyntax)node.Expression;

                if (memberAccessNode.Name.Identifier.Text == _collectionPartName)
                {
                    // 
                    // If identifier name equals to collection part name 
                    // we check whether are they the same symbols.
                    //

                    var nodeSymbol = _semanticModel.GetSymbolInfo(memberAccessNode).Symbol;

                    if (_collectionSymbol == nodeSymbol)
                    {
                        return _iterationIdentifier;
                    }
                }
            }

            return node;
        }
    }
}
