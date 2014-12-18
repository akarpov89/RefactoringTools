using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeRefactorings;
using Microsoft.CodeAnalysis.Text;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

using RefactoringTools;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;

namespace RefactoringToolsTest.LINQ
{
    public class LinqRefactoringsTest
    {
        delegate bool TryGetAction1(
            StatementSyntax statement, 
            out Func<SyntaxNode, SemanticModel, SyntaxNode> action
        );

        delegate bool TryGetAction2(
            StatementSyntax statement,
            SemanticModel semanticModel,
            out Func<SyntaxNode, SyntaxNode> action
        );

        [Fact]
        public void MergeSelectTest1()
        {
            
            var code =
@"
using System;
using System.Collections.Generic;
using System.Linq;

namespace Generated
{
    class Program
    {
        static int f(int x) => x;
        static in g(int x) => x;

        static void Main(string[] args)
        {
            var xs = new[] { 0 };

            var r = xs.Select(x => f(x)).Select(y => g(y)); 
        }
    }
}";
            var expected =
@"
using System;
using System.Collections.Generic;
using System.Linq;

namespace Generated
{
    class Program
    {
        static int f(int x) => x;
        static in g(int x) => x;

        static void Main(string[] args)
        {
            var xs = new[] { 0 };

            var r = xs.Select(x => g(f(x))); 
        }
    }
}";
            Verify(SelectMerger.TryGetAction, expected, code);
        }


        [Fact]
        public void MergeSelectTest2()
        {

            var code =
@"
using System;
using System.Collections.Generic;
using System.Linq;

namespace Generated
{
    class Program
    {
        static int f(int x) => x;
        static in g(int x) => x;

        static void Main(string[] args)
        {
            var xs = new[] { 0 };

            var r = xs.Select(f).Select(g);
        }
    }
}";
            var expected =
@"
using System;
using System.Collections.Generic;
using System.Linq;

namespace Generated
{
    class Program
    {
        static int f(int x) => x;
        static in g(int x) => x;

        static void Main(string[] args)
        {
            var xs = new[] { 0 };

            var r = xs.Select(x => g(f(x))); 
        }
    }
}";
            Verify(SelectMerger.TryGetAction, expected, code);
        }

        [Fact]
        public void SplitSelectTest1()
        {

            var expected =
@"
using System;
using System.Collections.Generic;
using System.Linq;

namespace Generated
{
    class Program
    {
        static int f(int x) => x;
        static in g(int x) => x;

        static void Main(string[] args)
        {
            var xs = new[] { 0 };

            var r = xs.Select(f).Select(g);
        }
    }
}";
            var code =
@"
using System;
using System.Collections.Generic;
using System.Linq;

namespace Generated
{
    class Program
    {
        static int f(int x) => x;
        static in g(int x) => x;

        static void Main(string[] args)
        {
            var xs = new[] { 0 };

            var r = xs.Select(x => g(f(x))); 
        }
    }
}";
            Verify(SelectSplitter.TryGetAction, expected, code);
        }

        private void Verify(
            TryGetAction1 tryGetAction,
            string expected, 
            string code
        )
        {
            var inputCompilation = CreateTestCompilation(code);
            var expectedCompilation = CreateTestCompilation(expected);

            var syntaxTree = inputCompilation.SyntaxTrees.First();
            var syntaxRoot = syntaxTree.GetRoot();

            var expectedSyntaxRoot = expectedCompilation.SyntaxTrees.First().GetRoot();

            var semanticModel = inputCompilation.GetSemanticModel(syntaxTree);

            var statement = FindTestResult(syntaxRoot);

            Func<SyntaxNode, SemanticModel, SyntaxNode> action = null;
            if (!tryGetAction(statement, out action))
            {
                throw new Exception("SelectMerger.TryGetAction returned false");
            }

            var outputSyntaxRoot = action(syntaxRoot, semanticModel);
            var outputStatement = FindTestResult(outputSyntaxRoot);

            Assert.Equal(
                Format(expectedSyntaxRoot).ToFullString(),
                Format(outputSyntaxRoot).ToFullString());
        }

        private void Verify(
            TryGetAction2 tryGetAction,
            string expected,
            string code
        )
        {
            var inputCompilation = CreateTestCompilation(code);
            var expectedCompilation = CreateTestCompilation(expected);

            var syntaxTree = inputCompilation.SyntaxTrees.First();
            var syntaxRoot = syntaxTree.GetRoot();

            var expectedSyntaxRoot = expectedCompilation.SyntaxTrees.First().GetRoot();

            var semanticModel = inputCompilation.GetSemanticModel(syntaxTree);

            var statement = FindTestResult(syntaxRoot);

            Func<SyntaxNode, SyntaxNode> action = null;
            if (!tryGetAction(statement, semanticModel, out action))
            {
                throw new Exception("SelectMerger.TryGetAction returned false");
            }

            var outputSyntaxRoot = action(syntaxRoot);
            var outputStatement = FindTestResult(outputSyntaxRoot);

            Assert.Equal(
                Format(expectedSyntaxRoot).ToFullString(),
                Format(outputSyntaxRoot).ToFullString());
        }

        private static SyntaxNode Format(SyntaxNode node)
        {
            return Formatter.Format(node, defaultWorkspace);
        }

        private static LocalDeclarationStatementSyntax FindTestResult(SyntaxNode root)
        {
            return root
                .DescendantNodes()
                .OfType<LocalDeclarationStatementSyntax>()
                .FirstOrDefault(d =>
                {
                    return d.Declaration.Variables[0].Identifier.Text == "r";
                });
        }

        private static Compilation CreateTestCompilation(string code)
        {
            MetadataReference[] references = { mscorlib };

            var syntaxTree = CSharpSyntaxTree.ParseText(code);

            return CSharpCompilation.Create(
                "Gen",
                new[] { syntaxTree },
                new[] { mscorlib }, 
                new CSharpCompilationOptions(OutputKind.ConsoleApplication));
        }        private static MetadataReference mscorlib = MetadataReference.CreateFromAssembly(typeof(object).Assembly);
        private static CustomWorkspace defaultWorkspace = new CustomWorkspace();
    }
}
