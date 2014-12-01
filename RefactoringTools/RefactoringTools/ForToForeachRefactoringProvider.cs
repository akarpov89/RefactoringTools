using Microsoft.CodeAnalysis.CodeRefactorings;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.Text;
using System.Threading;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace RefactoringTools
{
    [ExportCodeRefactoringProvider(RefactoringId, LanguageNames.CSharp)]
    internal class ForToForeachRefactoringProvider : ICodeRefactoringProvider
    {
        public const string RefactoringId = nameof(ForToForeachRefactoringProvider);

        public async Task<IEnumerable<CodeAction>> GetRefactoringsAsync(Document document, TextSpan span, CancellationToken cancellationToken)
        {
            var arr = new[] { 1, 2, 3 };

            for (var i = 0; i < arr.Count(); i++)
            {

            }

            var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);

            var node = root.FindNode(span);

            var statement = node as StatementSyntax;

            if (statement == null)
            {
                statement = node.TryFindParentStatement();
            }

            if (statement == null || !statement.IsKind(SyntaxKind.ForStatement))
                return null;

            var forStatement = (ForStatementSyntax)statement;

            var semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);

            bool isConvertiableToForeach = IsConvertibleToForeach(forStatement, semanticModel);

            return null;
        }

        private static bool IsConvertibleToForeach(ForStatementSyntax forStatement, SemanticModel semanticModel)
        {
            var declaration = forStatement.Declaration;
            var initializers = forStatement.Initializers;
            var condition = forStatement.Condition;
            var incrementors = forStatement.Incrementors;

            //
            // Initializers list must be empty;
            // Declaration must declare exactly one variable;
            // Condition must be "less than expression";
            // Incrementors list must have exactly one item which should be pre- or post-increment.
            //

            if (declaration == null 
                || initializers.Count != 0
                || condition == null || !condition.IsKind(SyntaxKind.LessThanExpression)
                || declaration.Variables.Count != 1
                || incrementors.Count != 1
                || (!incrementors[0].IsKind(SyntaxKind.PreIncrementExpression)
                 && !incrementors[0].IsKind(SyntaxKind.PostIncrementExpression)))
            {
                return false;
            }

            //
            // Declarations must be of type System.Int32
            //

            var typeSymbol = semanticModel.GetSymbolInfo(declaration.Type).Symbol as INamedTypeSymbol;
            if (typeSymbol == null || typeSymbol.SpecialType != SpecialType.System_Int32)
            {
                return false;
            }

            // Retrieve counter identifier
            var counterIdentifier = declaration.Variables[0].Identifier;

            //
            // Retrieve increment operand
            //

            ExpressionSyntax incrementOperand;

            if (incrementors[0].IsKind(SyntaxKind.PostIncrementExpression))
            {
                var postIncrement = (PostfixUnaryExpressionSyntax)incrementors[0];
                incrementOperand = postIncrement.Operand;
            }
            else
            {
                var preIncrement = (PrefixUnaryExpressionSyntax)incrementors[0];
                incrementOperand = preIncrement.Operand;
            }

            //
            // Increment operand must be identifier
            //

            if (!incrementOperand.IsKind(SyntaxKind.IdentifierName))
            {
                return false;
            }

            //
            // Increment operand must be the same as declared variable
            //

            var incrementOperandIdentifier = (IdentifierNameSyntax)incrementOperand;
            if (incrementOperandIdentifier.Identifier.Text != counterIdentifier.Text)
            {
                return false;
            }

            //
            // Retrieve less than expression
            //

            var lessThanCondition = (BinaryExpressionSyntax)condition;

            //
            // Left operand must be the same variable as declared variable
            //

            if (!lessThanCondition.Left.IsKind(SyntaxKind.IdentifierName))
            {
                return false;
            }

            var conditionLeftOperand = (IdentifierNameSyntax)lessThanCondition.Left;
            if (conditionLeftOperand.Identifier.Text != counterIdentifier.Text)
            {
                return false;
            }

            //
            // Process right operand.
            //

            ExpressionSyntax collectionExpression;
            SimpleNameSyntax lengthMember;

            //
            // Right operand must be simple member access expression (like xxx.Length or xxx.Count)
            // OR invocation expression like xxx.Count()
            //

            if (lessThanCondition.Right.IsKind(SyntaxKind.SimpleMemberAccessExpression))
            {
                var memberAccess = (MemberAccessExpressionSyntax) lessThanCondition.Right;
                collectionExpression = memberAccess.Expression;
                lengthMember = memberAccess.Name;
            }
            else if (lessThanCondition.Right.IsKind(SyntaxKind.InvocationExpression))
            {
                var invocation = (InvocationExpressionSyntax)lessThanCondition.Right;

                if (!invocation.Expression.IsKind(SyntaxKind.SimpleMemberAccessExpression)
                    || invocation.ArgumentList.Arguments.Count > 0)
                {
                    return false;
                }

                var memberAccess = (MemberAccessExpressionSyntax)lessThanCondition.Right;
                collectionExpression = memberAccess.Expression;
                lengthMember = memberAccess.Name;
            }
            else
            {
                return false;
            }

            //
            // Collection member name must be "Length" or "Count"
            //

            var lengthMemberName = lengthMember.Identifier.Text;
            if (lengthMemberName != "Length" && lengthMemberName != "Count")
            {
                return false;
            }

            var collectionType = semanticModel.GetTypeInfo(collectionExpression).Type;

            if (collectionType.TypeKind != TypeKind.ArrayType
                || !collectionType.AllInterfaces.Any(i => 
                        i.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) == 
                        "global::System.Collections.IEnumerable"
                   )
            )
            {
                return false;
            }

            //
            // Body of the loop mustn't modify collection elements and counter
            //

            // TODO Real Data Flow Analysis

            //
            // Traverse statements
            // save reads (access to symbol)
            // save writes.
            // Writes: x = e, x++, ++x, +=, -=, *=, /*, call to external method where this param is modified.
            //         Value type can be written through "ref" and "out" parameter call
            //

            return false;
        }
    }
}
