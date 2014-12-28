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
    internal static class CallsUnchainer
    {
        public static bool TryGetAction(
            StatementSyntax statement,
            out Func<SyntaxNode, SyntaxNode> action)
        {
            ExpressionSyntax outer = null;

            foreach (var node in statement.DescendantNodes())
            {
                if (node.IsKind(SyntaxKind.InvocationExpression)
                    || node.IsKind(SyntaxKind.ConditionalAccessExpression))
                {
                    var expression = (ExpressionSyntax)node;

                    int invocationsCount;

                    bool isExpressionMatch = IsMatch(expression, 0, true, out invocationsCount);

                    if (isExpressionMatch && invocationsCount >= 2)
                    {
                        outer = expression;
                        break;
                    }
                }
            }

            if (outer == null)
            {
                action = null;
                return false;
            }

            var declarations = new List<LocalDeclarationStatementSyntax>();
            
            while (outer.Parent.IsKind(SyntaxKind.ConditionalAccessExpression))
            {
                var conditonalAccess = (ConditionalAccessExpressionSyntax)outer.Parent;

                if (outer == conditonalAccess.WhenNotNull)
                {
                    outer = conditonalAccess;
                }
                else
                {
                    break;
                }
            }

            action = syntaxRoot => UnchainMultipleMethodCalls(syntaxRoot, outer);

            return true;
        }


        //-----------------------------------------------------------------------------------------------

        private static bool IsMatch(
            ExpressionSyntax expression, 
            int currentInvocationsCount,
            bool canStartWithNonInvocations,
            out int invocationsCount)
        {
            if (currentInvocationsCount >= 2 && canStartWithNonInvocations)
            {
                invocationsCount = 0;
                return true;
            }

            if (expression.IsKind(SyntaxKind.ConditionalAccessExpression))
            {
                return IsMatch(
                    (ConditionalAccessExpressionSyntax)expression,
                    currentInvocationsCount,
                    canStartWithNonInvocations,
                    out invocationsCount);
                
            }
            else if (expression.IsKind(SyntaxKind.InvocationExpression))
            {
                return IsMatch(
                    (InvocationExpressionSyntax)expression,
                    currentInvocationsCount,
                    canStartWithNonInvocations,
                    out invocationsCount);
            }
            else
            {
                invocationsCount = 0;
                return false;
            }
        }

        private static bool IsMatch(
            ConditionalAccessExpressionSyntax conditionalAccess,
            int currentInvocationsCount,
            bool canStartWithNonInvocations,
            out int invocationsCount)
        {
            invocationsCount = 0;

            if (conditionalAccess.WhenNotNull.IsKind(SyntaxKind.InvocationExpression))
            {
                var whenNotNullInvocation = 
                    (InvocationExpressionSyntax)conditionalAccess.WhenNotNull;

                int x;
                bool isWhenNotNullMatch = IsMatch(
                    whenNotNullInvocation, 
                    currentInvocationsCount + invocationsCount,
                    false, 
                    out x);

                if (!isWhenNotNullMatch)
                    return false;

                invocationsCount += x;

                if (currentInvocationsCount + invocationsCount >= 2 && canStartWithNonInvocations)
                    return true;

                int y;
                bool isInnerExpressionMatch = IsMatch(
                    conditionalAccess.Expression, 
                    currentInvocationsCount + invocationsCount,
                    canStartWithNonInvocations, 
                    out y);

                if (!isInnerExpressionMatch)
                    return false;

                invocationsCount += y;

                return true;
            }
            else if (conditionalAccess.WhenNotNull.IsKind(SyntaxKind.ConditionalAccessExpression))
            {
                var whenNotNullConditionalAccess = 
                    (ConditionalAccessExpressionSyntax)conditionalAccess.WhenNotNull;

                int x;
                bool isWhenNotNullMatch = IsMatch(
                    whenNotNullConditionalAccess,
                    currentInvocationsCount,
                    false, 
                    out x);

                if (!isWhenNotNullMatch)
                    return false;

                invocationsCount = x;

                if (currentInvocationsCount + invocationsCount >= 2 && canStartWithNonInvocations)
                    return true;

                int y;
                bool isInnerExpressionMatch = IsMatch(
                    conditionalAccess.Expression,
                    currentInvocationsCount + invocationsCount, 
                    canStartWithNonInvocations, 
                    out y);

                if (!isInnerExpressionMatch)
                    return false;

                invocationsCount += y;

                return true;
            }
            else
            {
                return false;
            }
        }

        private static bool IsMatch(
            InvocationExpressionSyntax invocation,
            int currentInvocationsCount,
            bool canStartWithNonInvocations,
            out int invocationsCount)
        {
            invocationsCount = 1;

            if (currentInvocationsCount + invocationsCount >= 2 && canStartWithNonInvocations)
                return true;

            ExpressionSyntax innerExpression;

            if (invocation.Expression.IsKind(SyntaxKind.MemberBindingExpression))
            {
                return true;
            }
            if (invocation.Expression.IsKind(SyntaxKind.SimpleMemberAccessExpression))
            {
                var memberAccess = (MemberAccessExpressionSyntax)invocation.Expression;
                innerExpression = memberAccess.Expression;
            }
            else if (invocation.Expression.IsKind(SyntaxKind.ConditionalAccessExpression))
            {
                innerExpression = invocation.Expression;
            }
            else
            {
                return false;
            }

            int x;

            bool isInnerExpressionMatch = IsMatch(
                innerExpression,
                currentInvocationsCount + invocationsCount,
                canStartWithNonInvocations,
                out x);

            if (!isInnerExpressionMatch)
                return false;

            invocationsCount += x;

            return true;
        }

        //-----------------------------------------------------------------------------------------------

        private static SyntaxNode UnchainMultipleMethodCalls(SyntaxNode syntaxRoot, ExpressionSyntax outer)
        {
            var declarations = new List<LocalDeclarationStatementSyntax>();

            var newOuter = Unchain(outer, declarations);

            var parentStatement = outer.TryFindParentStatement();
            var parentBlock = outer.TryFindParentBlock();

            var statements = parentBlock.Statements;
            var index = statements.IndexOf(parentStatement);

            syntaxRoot = syntaxRoot.ReplaceNodes(
                new SyntaxNode[] { outer, parentBlock }, 
                (oldNode, newNode) =>
                {
                    if (oldNode == outer)
                    {
                        return newOuter;
                    }
                    else if (oldNode == parentBlock)
                    {
                        var updatedBlock = newNode as BlockSyntax;

                        var newStatements = updatedBlock.Statements.InsertRange(index, declarations);

                        var newBlock = SyntaxFactory.Block(newStatements);

                        return newBlock;
                    }
                    else
                    {
                        return null;
                    }
                }
            );

            return syntaxRoot.Format();
        }

        private static bool IsEndsWithInvocation(ExpressionSyntax node)
        {
            if (node.IsKind(SyntaxKind.InvocationExpression))
                return true;

            if (!node.IsKind(SyntaxKind.ConditionalAccessExpression))
                return false;

            var conditionalAccess = (ConditionalAccessExpressionSyntax)node;

            do
            {
                var whenNotNull = conditionalAccess.WhenNotNull;

                if (whenNotNull.IsKind(SyntaxKind.InvocationExpression))
                    return true;

                if (!whenNotNull.IsKind(SyntaxKind.ConditionalAccessExpression))
                    return false;

                conditionalAccess = (ConditionalAccessExpressionSyntax)whenNotNull;

            } while (true);
        }

        private static IdentifierNameSyntax AddDeclaration(
            ExpressionSyntax value, List<LocalDeclarationStatementSyntax> declarations)
        {
            var equalsValue = SyntaxFactory.EqualsValueClause(value);

            var newVariableName = "newVar" + declarations.Count.ToString();

            var declarator = SyntaxFactory
                .VariableDeclarator(newVariableName)
                .WithInitializer(equalsValue);

            var typeSyntax = SyntaxFactory.IdentifierName("var");

            var variableDeclaration = SyntaxFactory.VariableDeclaration(
                typeSyntax,
                new SeparatedSyntaxList<VariableDeclaratorSyntax>().Add(declarator));

            var declarationStatement = SyntaxFactory.LocalDeclarationStatement(variableDeclaration);

            declarations.Add(declarationStatement);

            return SyntaxFactory.IdentifierName(declarator.Identifier);
        }

        private static ExpressionSyntax TryGetInnerInvocation(ExpressionSyntax expression)
        {
            if (expression.IsKind(SyntaxKind.SimpleMemberAccessExpression))
            {
                if (!expression.Parent.IsKind(SyntaxKind.InvocationExpression))
                {
                    return null;
                }

                var memberAccess = (MemberAccessExpressionSyntax)expression;

                if (!memberAccess.Expression.IsKind(SyntaxKind.InvocationExpression) &&
                    !memberAccess.Expression.IsKind(SyntaxKind.ConditionalAccessExpression))
                {
                    return null;
                }

                return memberAccess.Expression;
            }
            else if (expression.IsKind(SyntaxKind.InvocationExpression))
            {
                return expression;
            }
            else if (expression.IsKind(SyntaxKind.ConditionalAccessExpression))
            {
                var conditionalAccess = (ConditionalAccessExpressionSyntax)expression;

                if (!conditionalAccess.Expression.IsKind(SyntaxKind.ConditionalAccessExpression))
                {
                    if (conditionalAccess.Expression.IsKind(SyntaxKind.InvocationExpression))
                    {
                        return expression;
                    }

                    var invocation = (InvocationExpressionSyntax)conditionalAccess.WhenNotNull;

                    if (invocation.Expression.IsKind(SyntaxKind.MemberBindingExpression))
                        return null;
                }

                return expression;
            }

            return null;
        }

        private static ExpressionSyntax Reformat(ConditionalAccessExpressionSyntax expression)
        {
            if (!expression.WhenNotNull.IsKind(SyntaxKind.InvocationExpression))
            {
                return expression;
            }

            var invocation = (InvocationExpressionSyntax)expression.WhenNotNull;

            if (!expression.Expression.IsKind(SyntaxKind.ConditionalAccessExpression) &&
                !expression.Expression.IsKind(SyntaxKind.InvocationExpression) &&
                invocation.Expression.IsKind(SyntaxKind.SimpleMemberAccessExpression))
            {
                var memberAccess = (MemberAccessExpressionSyntax)invocation.Expression;

                var conditionalAccess = SyntaxFactory.ConditionalAccessExpression(
                    expression.Expression,
                    memberAccess.Expression);

                var newMemberAccess = SyntaxFactory.MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    conditionalAccess,
                    memberAccess.Name);

                return SyntaxFactory.InvocationExpression(newMemberAccess, invocation.ArgumentList);
            }
            else
            {
                return expression;
            }
        }

        private static ExpressionSyntax PeekLastVariableValue(
            List<LocalDeclarationStatementSyntax> declarations,
            out SyntaxToken variableName)
        {
            var lastDeclaration = declarations[declarations.Count - 1];
            var lastVariable = lastDeclaration.Declaration.Variables[0];

            variableName = lastVariable.Identifier;
            var variableValue = lastVariable.Initializer.Value;

            return variableValue;
        }

        private static ExpressionSyntax PopLastVariableValue(
            List<LocalDeclarationStatementSyntax> declarations)
        {
            SyntaxToken unused;
            var variableValue = PeekLastVariableValue(declarations, out unused);

            declarations.RemoveAt(declarations.Count - 1);

            return variableValue;
        }

        private static ExpressionSyntax UnrollAccess(
            ExpressionSyntax node, 
            List<LocalDeclarationStatementSyntax> declarations, 
            ref bool hasBinding)
        {
            if (node.IsKind(SyntaxKind.SimpleMemberAccessExpression))
            {
                var simple = (MemberAccessExpressionSyntax)node;

                var inner = UnrollAccess(simple.Expression, declarations, ref hasBinding);

                return simple.WithExpression(inner);
            }
            else if (node.IsKind(SyntaxKind.ElementBindingExpression)
                     || node.IsKind(SyntaxKind.MemberBindingExpression))
            {
                hasBinding = true;

                var variableValue = PopLastVariableValue(declarations);

                var conditionalAccess = SyntaxFactory.ConditionalAccessExpression(
                    variableValue,
                    node);

                while (declarations.Count > 0)
                {
                    variableValue = PopLastVariableValue(declarations);

                    conditionalAccess = SyntaxFactory.ConditionalAccessExpression(
                        variableValue,
                        conditionalAccess);
                }

                return conditionalAccess;
            }
            else if (node.IsKind(SyntaxKind.ElementAccessExpression))
            {
                var elementAccess = (ElementAccessExpressionSyntax)node;

                var expression = UnrollAccess(elementAccess.Expression, declarations, ref hasBinding);

                return elementAccess.WithExpression(expression);
            }
            else if (node.IsKind(SyntaxKind.InvocationExpression))
            {
                var invocation = (InvocationExpressionSyntax)node;

                var inner = UnrollAccess(invocation.Expression, declarations, ref hasBinding);

                return invocation.WithExpression(inner);
            }            
            else
            {
                return node;
            }
        }

        private static ExpressionSyntax Unchain(
            ExpressionSyntax outer, 
            List<LocalDeclarationStatementSyntax> declarations)
        {
            if (outer.IsKind(SyntaxKind.ConditionalAccessExpression))
            {
                var conditionalAccess = (ConditionalAccessExpressionSyntax)outer;

                var reformatted = Reformat(conditionalAccess);

                if (reformatted != conditionalAccess)
                {
                    return Unchain(reformatted, declarations);
                }

                if (!conditionalAccess.Expression.IsKind(SyntaxKind.ConditionalAccessExpression) &&
                    !conditionalAccess.Expression.IsKind(SyntaxKind.InvocationExpression) &&
                    !conditionalAccess.WhenNotNull.IsKind(SyntaxKind.ConditionalAccessExpression))
                {
                    return outer;
                }

                var innerInvocation = TryGetInnerInvocation(conditionalAccess.Expression);

                if (innerInvocation == null)
                {
                    AddDeclaration(conditionalAccess.Expression, declarations);                    
                }
                else
                {
                    var newInner = Unchain(innerInvocation, declarations);

                    AddDeclaration(newInner, declarations);
                }

                return Unchain(conditionalAccess.WhenNotNull, declarations);
            }
            else
            {
                var invocation = (InvocationExpressionSyntax)outer;

                if (invocation.Expression.IsKind(SyntaxKind.SimpleMemberAccessExpression))
                {
                    var innerInvocation = TryGetInnerInvocation(invocation.Expression);

                    if (innerInvocation == null)
                    {
                        var memberAccess = (MemberAccessExpressionSyntax)invocation.Expression;

                        bool hasBinding = false;

                        var unrolledAccess = UnrollAccess(memberAccess, declarations, ref hasBinding);

                        if (hasBinding)
                        {
                            var newInvocation = invocation.WithExpression(unrolledAccess);

                            return newInvocation;
                        }
                        
                        return outer;
                    }
                    else
                    {
                        var newInner = Unchain(innerInvocation, declarations);

                        var identifier = AddDeclaration(newInner, declarations);

                        var outerMemberAccess = (MemberAccessExpressionSyntax)invocation.Expression;

                        var memberAccess = SyntaxFactory.MemberAccessExpression(
                            SyntaxKind.SimpleMemberAccessExpression,
                            identifier,
                            outerMemberAccess.Name);

                        var newOuterInvocation = SyntaxFactory.InvocationExpression(
                            memberAccess, 
                            invocation.ArgumentList);

                        return newOuterInvocation;
                    }
                }
                else
                {
                    SyntaxToken identifierToken;
                    var variableValue = PeekLastVariableValue(declarations, out identifierToken);

                    ConditionalAccessExpressionSyntax newConditionalAccess;

                    if (!variableValue.IsKind(SyntaxKind.InvocationExpression)
                        && !variableValue.IsKind(SyntaxKind.ConditionalAccessExpression))
                    {
                        PopLastVariableValue(declarations);

                        newConditionalAccess = SyntaxFactory.ConditionalAccessExpression(
                            variableValue, 
                            invocation);

                        while (declarations.Count > 0)
                        {
                            variableValue = PopLastVariableValue(declarations);

                            newConditionalAccess = SyntaxFactory.ConditionalAccessExpression(
                                variableValue,
                                newConditionalAccess);
                        }
                    }
                    else
                    {
                        var identifier = SyntaxFactory.IdentifierName(identifierToken);

                        newConditionalAccess = SyntaxFactory.ConditionalAccessExpression(
                            identifier, 
                            invocation);
                    }

                    return newConditionalAccess;
                }
            }
        }        
    }
}
