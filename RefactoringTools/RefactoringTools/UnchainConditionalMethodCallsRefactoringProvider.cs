using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeRefactorings;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.Text;
using System.Threading;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.CSharp;

namespace RefactoringTools
{
    [ExportCodeRefactoringProvider(RefactoringId, LanguageNames.CSharp)]
    internal class UnchainConditionalMethodCallsRefactoringProvider : ICodeRefactoringProvider 
    {
        public const string RefactoringId = "UnchainConditionalMethodCallsRefactoringProvider";
        
        public async Task<IEnumerable<CodeAction>> GetRefactoringsAsync(
            Document document, TextSpan span, CancellationToken cancellationToken)
        {
            var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);

            var node = root.FindNode(span);

            var statement = node as StatementSyntax;

            if (statement == null)
            {
                statement = node.TryFindParentStatement();
            }

            if (statement == null || statement.IsKind(SyntaxKind.Block))
                return null;

            var outer = statement.DescendantNodes().FirstOrDefault(x =>
            {
                if (x.IsKind(SyntaxKind.ConditionalAccessExpression))
                {
                    var conditionalAccess = (ConditionalAccessExpressionSyntax)x;

                    // The last expression is always should be Invocation
                    if (!conditionalAccess.WhenNotNull.IsKind(SyntaxKind.InvocationExpression))
                        return false;

                    var accessesSource = conditionalAccess
                        .WhenNotNull
                        .DescendantNodes(n => !n.IsKind(SyntaxKind.ArgumentList));

                    var memberAccesses = accessesSource                        
                        .Where(n => n.IsKind(SyntaxKind.SimpleMemberAccessExpression))
                        .Cast<MemberAccessExpressionSyntax>()
                        .ToArray();

                    var conditionalAccesses = accessesSource
                        .Where(n => n.IsKind(SyntaxKind.ConditionalAccessExpression))
                        .Cast<ConditionalAccessExpressionSyntax>()
                        .ToArray();

                    if ((memberAccesses.Length + conditionalAccesses.Length) == 0)
                    {
                        // We have two subsequent invocations. That's enough.
                        return IsEndsWithInvocation(conditionalAccess.Expression);
                    }
                    else
                    {
                        // We should check that all member accesses are method

                        bool isAllInvocations = 
                            memberAccesses.All(ma => ma.Expression.IsKind(SyntaxKind.InvocationExpression)) &&
                            conditionalAccesses.All(ca => ca.WhenNotNull.IsKind(SyntaxKind.InvocationExpression));

                        return isAllInvocations;
                    }                    
                }
                else if (x.IsKind(SyntaxKind.InvocationExpression))
                {
                    // No conditional access. Check if we have two outer invocations

                    var outerInvocation = (InvocationExpressionSyntax)x;

                    if (!outerInvocation.Expression.IsKind(SyntaxKind.SimpleMemberAccessExpression))
                        return false;

                    var memberAccess = (MemberAccessExpressionSyntax)outerInvocation.Expression;

                    if (!memberAccess.Expression.IsKind(SyntaxKind.InvocationExpression))
                        return false;

                    return true;
                }
                else
                {
                    return false;
                }
            }) 
            as ExpressionSyntax;

            if (outer == null)
                return null;

            
            var declarations = new List<LocalDeclarationStatementSyntax>();

            var action = CodeAction.Create(
                "Unchain calls", 
                c => UnchainMultipleMethodCalls(document, outer, c));

            return new[] { action };
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

        private static IdentifierNameSyntax AddDeclaration(ExpressionSyntax value, List<LocalDeclarationStatementSyntax> declarations)
        {
            var equalsValue = SyntaxFactory.EqualsValueClause(value);

            var newVariableName = "newVar" + declarations.Count.ToString();

            var declarator = SyntaxFactory.VariableDeclarator(newVariableName).WithInitializer(equalsValue);
            //declarator = declarator.ReplaceToken(declarator.Identifier, declarator.Identifier.WithAdditionalAnnotations(MyRenameAnnotation));

            var typeSyntax = SyntaxFactory.IdentifierName("var");

            var variableDeclaration = SyntaxFactory.VariableDeclaration(
                typeSyntax, 
                new SeparatedSyntaxList<VariableDeclaratorSyntax>().Add(declarator));

            var declarationStatement = SyntaxFactory.LocalDeclarationStatement(variableDeclaration);

            declarations.Add(declarationStatement);

            return SyntaxFactory.IdentifierName(declarator.Identifier);
        }

        private ExpressionSyntax TryGetInnerInvocation(ExpressionSyntax expression)
        {
            if (expression.IsKind(SyntaxKind.SimpleMemberAccessExpression))
            {
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

        private ExpressionSyntax Reformat(ConditionalAccessExpressionSyntax expression)
        {
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

        private ExpressionSyntax Unchain(ExpressionSyntax outer, List<LocalDeclarationStatementSyntax> declarations)
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
                    !conditionalAccess.Expression.IsKind(SyntaxKind.InvocationExpression))
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

                        var newOuterInvocation = SyntaxFactory.InvocationExpression(memberAccess, invocation.ArgumentList);

                        return newOuterInvocation;
                    }
                }
                else
                {
                    var lastDeclaration = declarations.Last();
                    var identifierToken = lastDeclaration.Declaration.Variables[0].Identifier;
                    var identifier = SyntaxFactory.IdentifierName(identifierToken);


                    var newConditionalAccess = SyntaxFactory.ConditionalAccessExpression(identifier, invocation);

                    return newConditionalAccess;
                }
            }
        }

        private async Task<Solution> UnchainMultipleMethodCalls(
            Document document, ExpressionSyntax outer, CancellationToken cancellationToken)
        {
            var declarations = new List<LocalDeclarationStatementSyntax>();

            var newOuter = Unchain(outer, declarations);

            var parentStatement = outer.TryFindParentStatement();
            var parentBlock = outer.TryFindParentBlock();

            var statements = parentBlock.Statements;
            var index = statements.IndexOf(parentStatement);

            var syntaxRoot = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);

            syntaxRoot = syntaxRoot.ReplaceNodes(new SyntaxNode[] { outer, parentBlock }, (oldNode, newNode) =>
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
            });

            syntaxRoot = syntaxRoot.Format();

            return document.WithSyntaxRoot(syntaxRoot).Project.Solution;
        }
                
    }
}
