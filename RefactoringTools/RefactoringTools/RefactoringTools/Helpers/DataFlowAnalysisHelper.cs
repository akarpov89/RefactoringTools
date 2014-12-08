// Copyright (c) Andrew Karpov. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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

        public static bool IsLoopBodyReadsOnlyCurrentItem(
            StatementSyntax body, 
            SemanticModel semanticModel, 
            ExpressionSyntax collection, 
            string counterName)
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
            // Element access cannot be used in modifying expressions.
            //

            var elementAccessParent = elementAccess.Parent;
            var elementAccessParentKind = elementAccessParent.CSharpKind();

            if (elementAccessParentKind.IsModifyingUnaryExpression())
            {
                return false;
            }
            else if (elementAccessParentKind.IsModifyingBinaryExpression())
            {
                //
                // If element access parent is modifying binary expression,
                // then Left branch mustn't be our element access
                //

                var binaryExpression = (BinaryExpressionSyntax)elementAccessParent;

                if (binaryExpression.Left == elementAccess)
                {
                    return false;
                }
            }
            else if (elementAccessParentKind == SyntaxKind.Argument)
            {
                //
                // If accessed element is used as argument then 
                // we check if parameter is ref or out.
                //

                var argument = (ArgumentSyntax)elementAccessParent;

                if (argument != null 
                    && (argument.RefOrOutKeyword.IsKind(SyntaxKind.RefKeyword) 
                        || argument.RefOrOutKeyword.IsKind(SyntaxKind.OutKeyword)))
                {
                    return false;
                }
            }

            return true;
        }

        public static bool IsModifyingUnaryExpression(this SyntaxKind expressionKind)
        {
            switch (expressionKind)
            {
                case SyntaxKind.PreDecrementExpression:
                case SyntaxKind.PreIncrementExpression:
                case SyntaxKind.PostDecrementExpression:
                case SyntaxKind.PostIncrementExpression:
                    return true;
                default:
                    return false;
            }
        }

        public static bool IsModifyingBinaryExpression(this SyntaxKind expressionKind)
        {
            switch (expressionKind)
            {
                case SyntaxKind.SimpleAssignmentExpression:
                case SyntaxKind.AndAssignmentExpression:
                case SyntaxKind.MultiplyAssignmentExpression:
                case SyntaxKind.OrAssignmentExpression:
                case SyntaxKind.ExclusiveOrAssignmentExpression:
                case SyntaxKind.SubtractAssignmentExpression:
                case SyntaxKind.ModuloAssignmentExpression:
                case SyntaxKind.AddAssignmentExpression:
                case SyntaxKind.DivideAssignmentExpression:
                    return true;
                default:
                    return false;
            }
        }

        public static bool IsModifyingOperatorKind(this SyntaxKind operatorKind)
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

        public static bool HasLengthAndGetIndexer(ITypeSymbol collectionType, out string lengthMemberName)
        {
            lengthMemberName = null;

            if (collectionType.TypeKind == TypeKind.Array)
            {
                lengthMemberName = "Length";
                return true;
            }

            var members = collectionType.GetMembers();

            bool hasCount = false;
            bool hasIndexer = false;

            foreach (var member in collectionType.GetMembers())
            {
                if (member.Kind == SymbolKind.Property && member.Name == "Count")
                {
                    hasCount = true;
                    lengthMemberName = "Count";
                }

                if (member.Kind == SymbolKind.Method && member.Name == "get_Item")
                {
                    var getItemMethod = (IMethodSymbol)member;
                    if (getItemMethod.Parameters.Length == 1
                        && getItemMethod.Parameters[0].Type.SpecialType == SpecialType.System_Int32)
                    {
                        hasIndexer = true;
                    }
                }

                if (hasCount && hasIndexer)
                {
                    return true;
                }
            }

            return false;
        }

        public static bool IsIdentifierReferencedIn(
            string identifierName, 
            ISymbol identifierSymbol, 
            SemanticModel semanticModel,
            SyntaxNode searchArea)
        {
            foreach (var node in searchArea.DescendantNodes())
            {
                if (!node.IsKind(SyntaxKind.IdentifierName))
                    continue;

                var currentIdentifier = (IdentifierNameSyntax)node;

                if (currentIdentifier.Identifier.Text != identifierName)
                    continue;

                var currentIdentifierSymbol = semanticModel.GetSymbolInfo(currentIdentifier).Symbol;

                if (currentIdentifierSymbol == identifierSymbol)
                    return true;
            }

            return false;
        }
        
    }
}
