// Copyright (c) Andrew Karpov. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RefactoringTools
{
    /// <summary>
    /// Contains helper methods for work symbols.
    /// </summary>
    internal static class SymbolHelper
    {
        public static ITypeSymbol GetCollectionElementTypeSymbol(ITypeSymbol collectionType)
        {
            if (collectionType.TypeKind == TypeKind.Array)
            {
                var arrayType = (IArrayTypeSymbol)collectionType;
                return arrayType.ElementType;
            }

            foreach (var implementedInterface in collectionType.AllInterfaces)
            {
                if (!implementedInterface.IsGenericType)
                    continue;

                var interfaceName = implementedInterface.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

                if (interfaceName.StartsWith("global::System.Collections.Generic.IEnumerable"))
                {
                    return implementedInterface.TypeArguments[0];
                }
            }

            return null;
        }

        public static bool IsCollection(ITypeSymbol typeSymbol)
        {
            if (typeSymbol.Kind == SymbolKind.ArrayType)
                return true;

            foreach (var implementedInterface in typeSymbol.AllInterfaces)
            {
                if (!implementedInterface.IsGenericType)
                    continue;

                var interfaceName = implementedInterface.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

                if (interfaceName.StartsWith("global::System.Collections.Generic.IEnumerable"))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
