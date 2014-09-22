using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeRefactorings;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Rename;
using Microsoft.CodeAnalysis.Text;

namespace CodeRefactoring1
{
    [ExportCodeRefactoringProvider(StringExtensionRefactoringProvider.RefactoringId, LanguageNames.CSharp)]
    internal class StringExtensionRefactoringProvider : ICodeRefactoringProvider
    {
        public const string RefactoringId = "StringExtension.RefactoringId";

        public async Task<IEnumerable<CodeAction>> GetRefactoringsAsync(Document document, TextSpan textSpan, CancellationToken cancellationToken)
        {
            var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);

            var node = root.FindNode(textSpan);

            var invocation = node as InvocationExpressionSyntax;

            if (invocation == null &&
                node.Parent.IsKind(SyntaxKind.SimpleMemberAccessExpression) &&
                node.Parent.Parent.IsKind(SyntaxKind.InvocationExpression))
            {
                if (node.IsKind(SyntaxKind.PredefinedType))
                {
                    var predefinedTypeSyntax = node as PredefinedTypeSyntax;
                    if (predefinedTypeSyntax == null)
                        return null;

                    if (!predefinedTypeSyntax.Keyword.IsKind(SyntaxKind.StringKeyword))
                        return null;
                }
                else if (node.IsKind(SyntaxKind.IdentifierName))
                {
                    var identifier = node as IdentifierNameSyntax;
                    if (identifier.ToString() != "IsNullOrEmpty")
                        return null;
                }

                invocation = node.Parent.Parent as InvocationExpressionSyntax;
            }

            if (invocation == null || !IsRefactorable(invocation))
            {
                return null;
            }

            var action = CodeAction.Create("Replace with extension method", c => ReplaceWithExtensionMethod(document, invocation, c));

            return new[] { action };
        }

        private bool IsRefactorable(InvocationExpressionSyntax invocation)
        {
            var memberAccess = invocation.Expression as MemberAccessExpressionSyntax;

            if (!memberAccess.IsKind(SyntaxKind.SimpleMemberAccessExpression))
                return false;

            if (memberAccess.Name.ToString() != "IsNullOrEmpty")
                return false;

            var predefinedTypeSyntax = memberAccess.Expression as PredefinedTypeSyntax;
            if (predefinedTypeSyntax == null)
                return false;

            if (!predefinedTypeSyntax.Keyword.IsKind(SyntaxKind.StringKeyword))
                return false;

            var argumentList = invocation.ArgumentList;
            if (argumentList.Arguments.Count != 1)
                return false;

            return true;
        }

        private async Task<Solution> ReplaceWithExtensionMethod(Document document, InvocationExpressionSyntax invocation, CancellationToken cancellationToken)
        {
            var syntaxRoot = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
            var semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);

            var invocationArgument = invocation.ArgumentList.Arguments[0].Expression;
            var typeSymbol = semanticModel.GetTypeInfo(invocationArgument).Type;

            var hasExtensionMethodIsNullOrEmpty = semanticModel
                .LookupSymbols(invocationArgument.Span.End, typeSymbol, null, true)
                .OfType<IMethodSymbol>()
                .Any(s =>
                    s.IsExtensionMethod
                    && s.Name.EndsWith("IsNullOrEmpty")
                    && s.Parameters.Length == 0
                    && s.ReturnType.SpecialType == SpecialType.System_Boolean);

            var argumentList = invocation.ArgumentList;
            var argument = argumentList.Arguments[0].Expression;

            ExpressionSyntax target;

            if (argument is BinaryExpressionSyntax || argument is ConditionalExpressionSyntax)
            {
                target = SyntaxFactory.ParenthesizedExpression(argument);
            }
            else
            {
                target = argument;
            }


            var newMemberAccess = SyntaxFactory.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, target, SyntaxFactory.IdentifierName("IsNullOrEmpty"));
            var newInvocation = SyntaxFactory.InvocationExpression(newMemberAccess);

            syntaxRoot = syntaxRoot.ReplaceNode(invocation, newInvocation);

            if (!hasExtensionMethodIsNullOrEmpty)
            {
                var extensionClass = CreateStringExtensionsClass();

                var namespaceDeclaration = syntaxRoot.DescendantNodes().OfType<NamespaceDeclarationSyntax>().First();

                var newNamespaceDeclaration = namespaceDeclaration.AddMembers(extensionClass);

                var dump = newNamespaceDeclaration.ToString();

                syntaxRoot = syntaxRoot.ReplaceNode(namespaceDeclaration, newNamespaceDeclaration);
            }


            syntaxRoot = Microsoft.CodeAnalysis.Formatting.Formatter.Format(syntaxRoot, Microsoft.CodeAnalysis.MSBuild.MSBuildWorkspace.Create());

            document = document.WithSyntaxRoot(syntaxRoot);

            return document.Project.Solution;
        }

        private static ClassDeclarationSyntax CreateStringExtensionsClass()
        {
            var result =
            SyntaxFactory.ClassDeclaration("StringExtensions")
                .WithModifiers(new SyntaxTokenList().AddRange(
                    new SyntaxToken[]
                    {
                        SyntaxFactory.Token(SyntaxKind.InternalKeyword),
                        SyntaxFactory.Token(SyntaxKind.StaticKeyword)
                    }
                ))
                .WithMembers(new SyntaxList<MemberDeclarationSyntax>().Add(
                    CreateIsNullOrEmptyMethod()
                ));

            return result;
        }

        private static MethodDeclarationSyntax CreateIsNullOrEmptyMethod()
        {
            var argumentList =
                SyntaxFactory.ArgumentList(new SeparatedSyntaxList<ArgumentSyntax>().Add(
                    SyntaxFactory.Argument(SyntaxFactory.IdentifierName("input"))
                ));

            var argsToString = argumentList.ToString();

            var returnStatement =
                SyntaxFactory.ReturnStatement(
                    SyntaxFactory.InvocationExpression(
                        SyntaxFactory.MemberAccessExpression(
                            SyntaxKind.SimpleMemberAccessExpression,
                            SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.StringKeyword)),
                            SyntaxFactory.IdentifierName("IsNullOrEmpty")
                        ),
                        argumentList
                    )
                );

            var returnDump = returnStatement.ToString();


            var method =
                SyntaxFactory.MethodDeclaration(
                        SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.BoolKeyword)),
                        "IsNullOrEmpty")
                        .WithParameterList(SyntaxFactory.ParameterList(new SeparatedSyntaxList<ParameterSyntax>().Add(
                            SyntaxFactory.Parameter(SyntaxFactory.Identifier("input"))
                                .WithModifiers(new SyntaxTokenList().Add(
                                    SyntaxFactory.Token(SyntaxKind.ThisKeyword)
                                ))
                                .WithType(
                                    SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.StringKeyword))
                                ))))
                        .WithBody(SyntaxFactory.Block(new SyntaxList<StatementSyntax>().Add(
                            returnStatement
                        )))
                        .WithModifiers(new SyntaxTokenList().AddRange(
                            new SyntaxToken[]
                            {
                                SyntaxFactory.Token(SyntaxKind.PublicKeyword),
                                SyntaxFactory.Token(SyntaxKind.StaticKeyword)
                            }
                        ));

            var dump = method.ToString();

            return method;
        }
    }    
}