// Copyright (c) Andrew Karpov. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;

using Xunit;

using RefactoringTools;

using RefactoringToolsTest.TestHelper;


namespace RefactoringToolsTest.LINQ
{
    public class LinqRefactoringsTest
    {        
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
            VerifyDeclaration<StatementSyntax>(SelectMerger.TryGetAction, expected, code);
        }


        [Fact]
        public void MergeSelectTestWithMethodGroups()
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
        static int g(int x) => x;

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
        static int g(int x) => x;

        static void Main(string[] args)
        {
            var xs = new[] { 0 };

            var r = xs.Select(x => g(f(x))); 
        }
    }
}";
            VerifyDeclaration<StatementSyntax>(SelectMerger.TryGetAction, expected, code);
        }

        [Fact]
        public void SplitSelectTest()
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
        static int g(int x) => x;

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
        static int g(int x) => x;

        static void Main(string[] args)
        {
            var xs = new[] { 0 };

            var r = xs.Select(x => g(f(x))); 
        }
    }
}";
            VerifyDeclaration<StatementSyntax>(SelectSplitter.TryGetAction, expected, code);
        }

        [Fact]
        public void MergeWhereTest()
        {

            var code =
@"
using System;
using System.Collections.Generic;
using System.Linq;

namespace Generated
{
    class C
    {
        public static B = true;
    }
    class Program
    {
        static bool f(int x) => true;
        static bool g(int x) => false;

        static void Main(string[] args)
        {
            var xs = new[] { 0 };

            var r = xs.Where(f).Where(x => g(x)).Where(x => true).Where(x => C.B);
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
    class C
    {
        public static B = true;
    }
    class Program
    {
        static bool f(int x) => true;
        static bool g(int x) => false;

        static void Main(string[] args)
        {
            var xs = new[] { 0 };

            var r = xs.Where(x => f(x) && g(x) && true && C.B);
        }
    }
}";
            VerifyDeclaration<StatementSyntax>(WhereMerger.TryGetAction, expected, code);
        }

        [Fact]
        public void SplitWhereTest()
        {

            var expected =
@"
using System;
using System.Collections.Generic;
using System.Linq;

namespace Generated
{
    class C
    {
        public static B = true;
    }
    class Program
    {
        static bool f(int x) => true;
        static bool g(int x) => false;

        static void Main(string[] args)
        {
            var xs = new[] { 0 };

            var r = xs.Where(f).Where(g).Where(x => true).Where(x => C.B);
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
    class C
    {
        public static B = true;
    }
    class Program
    {
        static bool f(int x) => true;
        static bool g(int x) => false;

        static void Main(string[] args)
        {
            var xs = new[] { 0 };

            var r = xs.Where(x => f(x) && g(x) && true && C.B);
        }
    }
}";
            VerifyDeclaration<StatementSyntax>(WhereSplitter.TryGetAction, expected, code);
        }
    }
}
