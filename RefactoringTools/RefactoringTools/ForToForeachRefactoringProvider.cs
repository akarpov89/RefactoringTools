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
using System.Diagnostics.Contracts;

namespace RefactoringTools
{
    [ExportCodeRefactoringProvider(RefactoringId, LanguageNames.CSharp)]
    internal class ForToForeachRefactoringProvider : ICodeRefactoringProvider
    {
        public const string RefactoringId = nameof(ForToForeachRefactoringProvider);

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

            if (statement == null || !statement.IsKind(SyntaxKind.ForStatement))
                return null;

            var forStatement = (ForStatementSyntax)statement;

            var semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);

            if (!IsConvertibleToForeach(forStatement, semanticModel))
                return null;

            //return null;

            var action = CodeAction.Create(
                "Convert to foreach loop",
                c => ConvertToForeach(document, forStatement, c));

            return new[] { action };
        }

        private async Task<Document> ConvertToForeach(Document document, ForStatementSyntax forStatement, CancellationToken c)
        {
            var semanticModel = await document.GetSemanticModelAsync(c).ConfigureAwait(false);

            ExpressionSyntax collectionExpression;
            SimpleNameSyntax lengthMember;

            TryExtractCollectionInfo(
                (BinaryExpressionSyntax)forStatement.Condition, 
                out collectionExpression, 
                out lengthMember);

            var collectionType = semanticModel.GetTypeInfo(collectionExpression).Type;

            ITypeSymbol elementType = null;

            if (collectionType.TypeKind == TypeKind.ArrayType)
            {
                var arrayType = (IArrayTypeSymbol)collectionType;
                elementType = arrayType.ElementType;
            }
            else
            {
                var enumerableType = collectionType.AllInterfaces
                    .First(i => i.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
                                 .StartsWith("global::System.Collections.Generic.IEnumerable")
                             && i.IsGenericType);

                elementType = enumerableType.TypeArguments[0];
            }

            string elementTypeName = elementType.ToMinimalDisplayString(semanticModel, forStatement.SpanStart);

            ISymbol collectionSymbol;
            string collectionPartName;

            DataFlowAnalysisHelper.TryGetCollectionInfo(
                collectionExpression, 
                semanticModel, 
                out collectionPartName, 
                out collectionSymbol);

            var iterationIdentifier = SyntaxFactory
                .IdentifierName("iterVar")
                .WithAdditionalAnnotations(RenameAnnotation.Create());

            var rewriter = new ForToForeachLoopBodyRewriter(
                iterationIdentifier, 
                collectionPartName, 
                collectionSymbol, 
                semanticModel);

            var newBody = (StatementSyntax) forStatement.Statement.Accept(rewriter);

            var foreachStatement = SyntaxFactory.ForEachStatement(
                SyntaxFactory.ParseTypeName(elementTypeName), 
                iterationIdentifier.Identifier.WithAdditionalAnnotations(RenameAnnotation.Create()), 
                collectionExpression, 
                newBody);

            foreachStatement = foreachStatement
                .WithLeadingTrivia(forStatement.GetLeadingTrivia())
                .WithTrailingTrivia(forStatement.GetTrailingTrivia());

            var syntaxRoot = await document.GetSyntaxRootAsync(c).ConfigureAwait(false);

            syntaxRoot = syntaxRoot.ReplaceNode((SyntaxNode)forStatement, foreachStatement);

            syntaxRoot = syntaxRoot.Format();

            return document.WithSyntaxRoot(syntaxRoot);
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

            if (!TryExtractCollectionInfo(lessThanCondition, out collectionExpression, out lengthMember))
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
                && !collectionType.AllInterfaces.Any(i => 
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

            bool isLoopBodyOnlyReadsCurrentItem = DataFlowAnalysisHelper.IsLoopBodyReadsOnlyCurrentItem(
                forStatement.Statement, 
                semanticModel, 
                collectionExpression, 
                counterIdentifier.Text);            

            return isLoopBodyOnlyReadsCurrentItem;
        }

        private static bool TryExtractCollectionInfo(
            BinaryExpressionSyntax lessThanCondition, 
            out ExpressionSyntax collectionExpression, 
            out SimpleNameSyntax lengthMember)
        {
            collectionExpression = null;
            lengthMember = null;

            //
            // Right operand must be simple member access expression (like xxx.Length or xxx.Count)
            // OR invocation expression like xxx.Count()
            //

            if (lessThanCondition.Right.IsKind(SyntaxKind.SimpleMemberAccessExpression))
            {
                var memberAccess = (MemberAccessExpressionSyntax)lessThanCondition.Right;
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

            return true;
        }
    }

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
