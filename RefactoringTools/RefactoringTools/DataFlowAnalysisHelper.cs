using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.FindSymbols;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RefactoringTools
{
    internal static class DataFlowAnalysisHelper
    {
        public static bool TryGetCollectionInfo(
            ExpressionSyntax collection, 
            SemanticModel semanticModel, 
            out string collectionPartName, 
            out ISymbol collectionSymbol)
        {
            collectionPartName = null;
            collectionSymbol = null;

            //
            // Retrieve collection symbol
            //

            collectionSymbol = semanticModel.GetSymbolInfo(collection).Symbol;

            if (collectionSymbol == null)
            {
                return false;
            }

            //
            // Collection must be variable OR member access.
            // We want to get only part of name for further comparison.
            //

            if (collection.IsKind(SyntaxKind.IdentifierName))
            {
                collectionPartName = ((IdentifierNameSyntax)collection).Identifier.Text;
            }
            else if (collection.IsKind(SyntaxKind.SimpleMemberAccessExpression))
            {
                var collectionMemberAccess = (MemberAccessExpressionSyntax)collection;
                collectionPartName = collectionMemberAccess.Name.Identifier.Text;
            }
            else
            {
                return false;
            }

            return true;
        }

        public static bool IsLoopBodyReadsOnlyCurrentItem(StatementSyntax body, SemanticModel semanticModel, ExpressionSyntax collection, string counterName)
        {
            ISymbol collectionSymbol;
            string collectionPartName;

            if (!TryGetCollectionInfo(collection, semanticModel, out collectionPartName, out collectionSymbol))
            {
                return false;
            }

            //
            // Search for collection references within loop body 
            // (check only identifiers and member access elements)
            //

            foreach (var node in body.DescendantNodes())
            {                
                if (node.IsKind(SyntaxKind.IdentifierName))
                {
                    var identifierNode = (IdentifierNameSyntax)node;

                    if (identifierNode.Identifier.Text == collectionPartName)
                    {
                        // 
                        // If identifier name equals to collection part name 
                        // we check whether are they the same symbols.
                        //

                        var nodeSymbol = semanticModel.GetSymbolInfo(node).Symbol;

                        if (collectionSymbol == nodeSymbol)
                        {
                            // 
                            // If it's is a reference then we check 
                            // wheter it's read only element access by counter
                            //

                            if (!IsReadOnlyCollectionAccess(identifierNode, counterName))
                            {
                                return false;
                            }
                        }
                    }
                    else if (identifierNode.Identifier.Text == counterName)
                    {
                        //
                        // If identifier name equals to counter name
                        // we check whether counter is used for element access
                        //

                        if (!node.Parent.IsKind(SyntaxKind.Argument) 
                            || !node.Parent.Parent.IsKind(SyntaxKind.BracketedArgumentList)
                            || !node.Parent.Parent.Parent.IsKind(SyntaxKind.ElementAccessExpression))
                        {
                            return false;
                        }

                        //
                        // Check whether accessed by counter name collection is our collection
                        //

                        var elementAccess = (ElementAccessExpressionSyntax)node.Parent.Parent.Parent;

                        var accessedCollectionSymbol = semanticModel.GetSymbolInfo(elementAccess.Expression).Symbol;

                        if (accessedCollectionSymbol != collectionSymbol)
                        {
                            return false;
                        }
                    }

                }
                else if (node.IsKind(SyntaxKind.SimpleMemberAccessExpression))
                {
                    var memberAccessNode = (MemberAccessExpressionSyntax)node;                    

                    if (memberAccessNode.Name.Identifier.Text == collectionPartName)
                    {
                        // 
                        // If identifier name equals to collection part name 
                        // we check whether are they the same symbols.
                        //

                        var nodeSymbol = semanticModel.GetSymbolInfo(node).Symbol;

                        if (collectionSymbol == nodeSymbol)
                        {
                            // 
                            // If it's is a reference then we check 
                            // wheter it's read only element access by counter
                            //

                            if (!IsReadOnlyCollectionAccess(memberAccessNode, counterName))
                            {
                                return false;
                            }
                        }
                    }
                }                
            }

            return true;
        }

        private static bool IsReadOnlyCollectionAccess(ExpressionSyntax collection, string counterName)
        {
            //
            // Collection reference must be element access.
            //

            if (!collection.Parent.IsKind(SyntaxKind.ElementAccessExpression))
            {
                return false;
            }

            //
            // We are interested only in element accesses with single argument.
            //

            var elementAccess = (ElementAccessExpressionSyntax)collection.Parent;

            if (elementAccess.ArgumentList.Arguments.Count != 1)
            {
                return false;
            }

            //
            // Index must be equal to counter name
            //

            var indexArgument = elementAccess.ArgumentList.Arguments[0].Expression;

            if (!indexArgument.IsKind(SyntaxKind.IdentifierName))
            {
                return false;
            }

            if (((IdentifierNameSyntax)indexArgument).Identifier.Text != counterName)
            {
                return false;
            }

            //
            // Element access cannot be used in modifying expressions. Check for that.
            //

            //
            // If element access parent is binary expression and left operand is our element access, 
            // then check if operator modifies accessed element
            //

            var binaryExpression = elementAccess.Parent as BinaryExpressionSyntax;
            
            if (binaryExpression != null)
            {
                if (binaryExpression.Left == elementAccess
                    && IsModifyingOperatorKind(binaryExpression.OperatorToken.CSharpKind()))
                {
                    return false;
                }
            }
            else if (elementAccess.Parent.IsKind(SyntaxKind.Argument))
            {
                var argument = (ArgumentSyntax)elementAccess.Parent;

                if (argument != null 
                    && (argument.RefOrOutKeyword.IsKind(SyntaxKind.RefKeyword) 
                        || argument.RefOrOutKeyword.IsKind(SyntaxKind.OutKeyword)))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool IsModifyingOperatorKind(SyntaxKind operatorKind)
        {
            switch (operatorKind)
            {
                case SyntaxKind.EqualsToken:
                case SyntaxKind.AmpersandEqualsToken:
                case SyntaxKind.AsteriskEqualsToken:
                case SyntaxKind.BarEqualsToken:
                case SyntaxKind.CaretEqualsToken:
                case SyntaxKind.MinusEqualsToken:
                case SyntaxKind.PercentEqualsToken:
                case SyntaxKind.PlusEqualsToken:
                case SyntaxKind.SlashEqualsToken:
                    return true;
                default:
                    return false;
            }
        }
    }
}
