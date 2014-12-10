// Copyright (c) Andrew Karpov. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RefactoringTools
{
    /// <summary>
    /// Contains helper methods for generating names
    /// in different contexts.
    /// </summary>
    internal static class NameHelper
    {
        public static string GetIterationVariableName(
            string collectionName, 
            ITypeSymbol elementType, 
            int loopBodyPosition, 
            SemanticModel semanticModel)
        {
            if (collectionName.EndsWith("s") && collectionName.Length > 1)
            {
                string name = VarNameFromCollectionName(collectionName);

                if (IsUniqueName(name, loopBodyPosition, semanticModel))
                {
                    return name;
                }
            }

            if (!elementType.IsAnonymousType 
                && elementType.SpecialType == SpecialType.None 
                && elementType.Name.Length > 0) 
            {
                string name = VarNameFromTypeName(elementType.Name);

                if (IsUniqueName(name, loopBodyPosition, semanticModel))
                {
                    return name;
                }
            }

            if (IsUniqueName("item", loopBodyPosition, semanticModel))
                return "item";

            if (IsUniqueName("x", loopBodyPosition, semanticModel))
                return "x";

            return "ITEM";
        }

        public static string GetLoopCounterName(int loopBodyPosition, SemanticModel semanticModel)
        {
            if (IsUniqueName("i", loopBodyPosition, semanticModel))
                return "i";

            if (IsUniqueName("k", loopBodyPosition, semanticModel))
                return "k";

            if (IsUniqueName("j", loopBodyPosition, semanticModel))
                return "j";

            if (IsUniqueName("index", loopBodyPosition, semanticModel))
                return "index";

            if (IsUniqueName("counter", loopBodyPosition, semanticModel))
                return "counter";

            return "COUNTER";
        }

        public static string GetLambdaParameterName(int position, SemanticModel semanticModel)
        {
            if (IsUniqueName("x", position, semanticModel))
                return "x";

            if (IsUniqueName("arg", position, semanticModel))
                return "arg";

            if (IsUniqueName("item", position, semanticModel))
                return "item";

            return "ARG";
        }

        private static bool IsUniqueName(string name, int position, SemanticModel semanticModel)
        {
            var expressionSymbolInfo = semanticModel.GetSpeculativeSymbolInfo(
                position,
                SyntaxFactory.IdentifierName(name),
                SpeculativeBindingOption.BindAsExpression);

            if (expressionSymbolInfo.Symbol != null)
                return false;

            var typeSymbolInfo = semanticModel.GetSpeculativeSymbolInfo(
                position,
                SyntaxFactory.IdentifierName(name),
                SpeculativeBindingOption.BindAsTypeOrNamespace);

            return typeSymbolInfo.Symbol == null;
        }

        private static string VarNameFromCollectionName(string collectionName)
        {
            string name = Char.ToLowerInvariant(collectionName[0]).ToString()
                            + (collectionName.Length == 2
                               ? ""
                               : collectionName.Substring(1, collectionName.Length - 2));

            return name;
        }

        private static string VarNameFromTypeName(string typeName)
        {
            string name = Char.ToLowerInvariant(typeName[0]).ToString()
                        + (typeName.Length == 1
                           ? ""
                           : typeName.Substring(1));

            return name;
        }
    }
}
