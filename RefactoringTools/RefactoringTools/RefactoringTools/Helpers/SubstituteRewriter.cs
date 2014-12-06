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
    internal sealed class SubstituteRewriter : CSharpSyntaxRewriter
    {
        private readonly string identifierName;
        private readonly ISymbol identifierSymbol;
        private readonly SemanticModel semanticModel;
        private readonly ExpressionSyntax replacement;

        public SubstituteRewriter(
            string identifierName,
            ISymbol identifierSymbol,
            SemanticModel semanticModel,
            ExpressionSyntax replacement)
        {
            this.identifierName = identifierName;
            this.identifierSymbol = identifierSymbol;
            this.semanticModel = semanticModel;
            this.replacement = replacement;
        }

        public override SyntaxNode VisitIdentifierName(IdentifierNameSyntax node)
        {
            if (node.Identifier.Text == this.identifierName)
            {
                var currentIdentifierSymbol = this.semanticModel.GetSymbolInfo(node).Symbol;

                if (currentIdentifierSymbol == this.identifierSymbol)
                {
                    return replacement;
                }
            }

            return node;
        }
    }
}
