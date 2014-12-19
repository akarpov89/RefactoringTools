// Copyright (c) Andrew Karpov. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace RefactoringToolsTest
{
    delegate bool TryGetAction1<in TStatement>(
        TStatement statement,
        out Func<SyntaxNode, SemanticModel, SyntaxNode> action
    )
        where TStatement : StatementSyntax;

    delegate bool TryGetAction2<in TStatement>(
        TStatement statement,
        SemanticModel semanticModel,
        out Func<SyntaxNode, SyntaxNode> action
    )
        where TStatement : StatementSyntax;

    delegate bool TryGetAction3<in TStatement>(
        TStatement statement,
        out Func<SyntaxNode, SyntaxNode> action
    )
        where TStatement : StatementSyntax;

    delegate SyntaxNode StatementTransformer<TStatement>(
        TStatement inputStatement,
        SyntaxNode root,
        SemanticModel semanticModel
    ) where TStatement : StatementSyntax;    

    internal static class TestHelper
    {
        public static StatementTransformer<TStatement> AsTransformer<TStatement>(
            this TryGetAction1<TStatement> tryGetAction
        ) 
            where TStatement : StatementSyntax
        {
            return (inputStatement, root, semanticModel) =>
            {
                Func<SyntaxNode, SemanticModel, SyntaxNode> action;

                if (!tryGetAction(inputStatement, out action))
                    throw new Exception("tryGetAction returned false");

                return action(root, semanticModel);
            };
        }

        public static StatementTransformer<TStatement> AsTransformer<TStatement>(
            this TryGetAction2<TStatement> tryGetAction
        )
            where TStatement : StatementSyntax
        {
            return (inputStatement, root, semanticModel) =>
            {
                Func<SyntaxNode, SyntaxNode> action;

                if (!tryGetAction(inputStatement, semanticModel, out action))
                    throw new Exception("tryGetAction returned false");

                return action(root);
            };
        }

        public static StatementTransformer<TStatement> AsTransformer<TStatement>(
            this TryGetAction3<TStatement> tryGetAction
        )
            where TStatement : StatementSyntax
        {
            return (inputStatement, root, semanticModel) =>
            {
                Func<SyntaxNode, SyntaxNode> action;

                if (!tryGetAction(inputStatement, out action))
                    throw new Exception("tryGetAction returned false");

                return action(root);
            };
        }

        public static SyntaxNode Format(this SyntaxNode node)
        {
            return Formatter.Format(node, defaultWorkspace);
        }

        public static TStatement FindFixture<TStatement>(SyntaxNode root)
        {
            return root
                .DescendantNodes()
                .OfType<TStatement>()
                .FirstOrDefault();
        }

        public static StatementSyntax FindDeclaration(SyntaxNode root, string variableName = "r")
        {
            return root
                .DescendantNodes()
                .OfType<LocalDeclarationStatementSyntax>()
                .FirstOrDefault(d =>
                {
                    return d.Declaration.Variables[0].Identifier.Text == variableName;
                });
        }

        public static Compilation CreateTestCompilation(string code)
        {
            MetadataReference[] references = { mscorlib };

            var syntaxTree = CSharpSyntaxTree.ParseText(code);

            return CSharpCompilation.Create(
                "Gen",
                new[] { syntaxTree },
                new[] { mscorlib },
                new CSharpCompilationOptions(OutputKind.ConsoleApplication));
        }

        public static void Verify<TInput, TOutput>(
            StatementTransformer<TInput> transform,
            Func<SyntaxNode, TInput> findInput,
            Func<SyntaxNode, TOutput> findOutput,
            string expected,
            string code
        )
            where TInput : StatementSyntax
            where TOutput : StatementSyntax
        {
            var inputCompilation = CreateTestCompilation(code);
            var expectedCompilation = CreateTestCompilation(expected);

            var syntaxTree = inputCompilation.SyntaxTrees.First();
            var syntaxRoot = syntaxTree.GetRoot();

            var expectedSyntaxRoot = expectedCompilation.SyntaxTrees.First().GetRoot();
            var expectedOutput = findOutput(expectedSyntaxRoot);

            var semanticModel = inputCompilation.GetSemanticModel(syntaxTree);

            var statement = findInput(syntaxRoot);

            var outputSyntaxRoot = transform(statement, syntaxRoot, semanticModel);
            var outputStatement = findOutput(outputSyntaxRoot);

            Assert.Equal(
                Format(expectedOutput).ToFullString().Trim(),
                Format(outputStatement).ToFullString().Trim());
        }        

        public static void VerifyDeclaration<TStatement>(
            TryGetAction1<TStatement> tryGetAction,
            string expected,
            string code,
            string variableName = "r"
        ) where TStatement : StatementSyntax
        {
            Func<SyntaxNode, TStatement> findDeclaration = root => (TStatement)FindDeclaration(root, variableName);

            Verify(
                tryGetAction.AsTransformer(), 
                findDeclaration, 
                findDeclaration, 
                expected, 
                code
            );
        }

        public static void VerifyDeclaration<TStatement>(
            TryGetAction2<TStatement> tryGetAction,
            string expected,
            string code,
            string variableName = "r"
        ) where TStatement : StatementSyntax
        {
            Func<SyntaxNode, TStatement> findDeclaration = root => (TStatement)FindDeclaration(root, variableName);

            Verify(
                tryGetAction.AsTransformer(), 
                findDeclaration, 
                findDeclaration, 
                expected, 
                code
            );
        }

        public static void VerifyDeclaration<TStatement>(
            TryGetAction3<TStatement> tryGetAction,
            string expected,
            string code,
            string variableName = "r"
        ) where TStatement : StatementSyntax
        {
            Func<SyntaxNode, TStatement> findDeclaration = root => (TStatement)FindDeclaration(root, variableName);

            Verify(
                tryGetAction.AsTransformer(), 
                findDeclaration, 
                findDeclaration, 
                expected, 
                code
            );
        }

        public static void Verify<TInput, TOutput>(
            TryGetAction1<TInput> tryGetAction,
            string expected,
            string code
        ) 
            where TInput : StatementSyntax
            where TOutput : StatementSyntax
        {
            Verify(
                tryGetAction.AsTransformer(), 
                root => FindFixture<TInput>(root),
                root => FindFixture<TOutput>(root),
                expected, 
                code
            );
        }

        public static void Verify<TInput, TOutput>(
            TryGetAction2<TInput> tryGetAction,
            string expected,
            string code
        )
            where TInput : StatementSyntax
            where TOutput : StatementSyntax
        {
            Verify(
                tryGetAction.AsTransformer(),
                root => FindFixture<TInput>(root),
                root => FindFixture<TOutput>(root),
                expected,
                code
            );
        }

        public static void Verify<TInput, TOutput>(
            TryGetAction3<TInput> tryGetAction,
            string expected,
            string code
        )
            where TInput : StatementSyntax
            where TOutput : StatementSyntax
        {
            Verify(
                tryGetAction.AsTransformer(),
                root => FindFixture<TInput>(root),
                root => FindFixture<TOutput>(root),
                expected,
                code
            );
        }

        private static MetadataReference mscorlib = MetadataReference.CreateFromAssembly(typeof(object).Assembly);
        private static CustomWorkspace defaultWorkspace = new CustomWorkspace();
    }
}
