// Copyright (c) Andrew Karpov. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Xunit;

using RefactoringTools;
using RefactoringToolsTest.TestHelper;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace RefactoringToolsTest.LoopRefactorings
{
    public class LoopRefactoringsTest
    {
        [Fact]
        public void ForeachToForTest()
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
        static void Main(string[] args)
        {
            foreach (var arg in args)
            {
                var s = arg + arg;
                Console.WriteLine(arg);
            }
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
        static void Main(string[] args)
        {
            for (int i = 0; i < args.Length; i++)
            {                
                var s = args[i] + args[i];
                Console.WriteLine(args[i]);
            }
        }
    }
}";
            Verify<ForEachStatementSyntax, ForStatementSyntax>(ForEachToForTransformer.TryGetAction, expected, code);
        }

        [Fact]
        public void ForToForeachTest()
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
        static void Main(string[] args)
        {
            for (int i = 0; i < args.Length; i++)
            {                
                var s = args[i] + args[i];
                Console.WriteLine(args[i]);
            }
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
        static void Main(string[] args)
        {
            foreach (string arg in args)
            {
                var s = arg + arg;
                Console.WriteLine(arg);
            }
        }
    }
}";
            Verify<ForStatementSyntax, ForEachStatementSyntax>(ForToForEachTransformer.TryGetAction, expected, code);
        }
    }
}
