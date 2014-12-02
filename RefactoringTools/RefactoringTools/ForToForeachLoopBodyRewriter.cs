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
    internal class ForToForeachLoopBodyRewriter : CSharpSyntaxRewriter
    {
        private IdentifierNameSyntax iterationIdentifier;
        string collectionPartName;
        ISymbol collectionSymbol;
        SemanticModel semanticModel;

        public ForToForeachLoopBodyRewriter(
            IdentifierNameSyntax iterationIdentifier,
            string collectionPartName,
            ISymbol collectionSymbol,
            SemanticModel semanticModel)
        {
            this.iterationIdentifier = iterationIdentifier;
            this.collectionPartName = collectionPartName;
            this.collectionSymbol = collectionSymbol;
            this.semanticModel = semanticModel;
        }

        public override SyntaxNode VisitElementAccessExpression(ElementAccessExpressionSyntax node)
        {
            if (node.Expression.IsKind(SyntaxKind.IdentifierName))
            {
                var identifierNode = (IdentifierNameSyntax)node.Expression;

                if (identifierNode.Identifier.Text == collectionPartName)
                {
                    // 
                    // If identifier name equals to collection part name 
                    // we check whether are they the same symbols.
                    //

                    var nodeSymbol = semanticModel.GetSymbolInfo(identifierNode).Symbol;

                    if (collectionSymbol == nodeSymbol)
                    {
                        return iterationIdentifier;
                    }
                }
            }
            else if (node.Expression.IsKind(SyntaxKind.SimpleMemberAccessExpression))
            {
                var memberAccessNode = (MemberAccessExpressionSyntax)node.Expression;

                if (memberAccessNode.Name.Identifier.Text == collectionPartName)
                {
                    // 
                    // If identifier name equals to collection part name 
                    // we check whether are they the same symbols.
                    //

                    var nodeSymbol = semanticModel.GetSymbolInfo(memberAccessNode).Symbol;

                    if (collectionSymbol == nodeSymbol)
                    {
                        return iterationIdentifier;
                    }
                }
            }

            return node;
        }
    }
}
