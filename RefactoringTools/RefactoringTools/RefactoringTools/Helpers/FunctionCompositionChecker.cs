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
    internal sealed class FunctionCompositionChecker
    {
        private readonly string _parameterName;
        private readonly ISymbol _parameterSymbol;
        private readonly SemanticModel _semanticModel;
        private readonly Stack<InvocationExpressionSyntax> _invocationStack;

        public FunctionCompositionChecker(
            string parameterName,
            ISymbol parameterSymbol,
            SemanticModel semanticModel)
        {
            _parameterName = parameterName;
            _parameterSymbol = parameterSymbol;
            _semanticModel = semanticModel;
            _invocationStack = new Stack<InvocationExpressionSyntax>();
        }

        public bool IsComposition(
            InvocationExpressionSyntax invocation,
            out Stack<InvocationExpressionSyntax> invocationStack)
        {
            invocationStack = null;

            _invocationStack.Clear();

            if (!Process(invocation))
                return false;

            invocationStack = _invocationStack;
            return true;
        }

        private bool Process(InvocationExpressionSyntax invocation)
        {
            if (IsLeafInvocation(invocation))
            {
                _invocationStack.Push(invocation);
                return true;
            }

            if (invocation.Expression != null && IsParameterReferencedIn(invocation.Expression))
            {
                return false;
            }

            InvocationExpressionSyntax innerInvocation = null;

            foreach (var argument in invocation.ArgumentList.Arguments)
            {
                if (argument.Expression.IsKind(SyntaxKind.InvocationExpression))
                {
                    if (innerInvocation == null)
                    {
                        innerInvocation = (InvocationExpressionSyntax)argument.Expression;
                    }
                    else if (!argument.Expression.IsEquivalentTo(innerInvocation))
                    {
                        return false;
                    }
                }
                else if (IsParameterReferencedIn(argument.Expression))
                {
                    return false;
                }
            }

            _invocationStack.Push(invocation);

            return Process(innerInvocation);
        }

        private bool IsLeafInvocation(InvocationExpressionSyntax invocation)
        {
            foreach (var argument in invocation.ArgumentList.Arguments)
            {
                if (!argument.Expression.IsKind(SyntaxKind.InvocationExpression))
                    continue;

                if (IsParameterReferencedIn(argument))
                    return false;
            }

            return true;
        }

        private bool IsParameterReferencedIn(SyntaxNode searchArea)
        {
            return DataFlowAnalysisHelper.IsIdentifierReferencedIn(
                _parameterName,
                _parameterSymbol,
                _semanticModel,
                searchArea);
        }
    }
}
