using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Microsoft.CodeAnalysis.CSharp.Syntax;

using Xunit;

using RefactoringTools;
using RefactoringToolsTest.TestHelper;
using Microsoft.CodeAnalysis;

namespace RefactoringToolsTest.MethodCallsRefactorings
{
    public class CallRefactoringsTest
    {
        private static bool Chain(BlockSyntax block, out Func<SyntaxNode, SyntaxNode> action)
        {
            return CallsChainer.TryGetAction(block, block.Statements.ToList(), out action);
        }

        [Fact]
        public void ChainCalls()
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
            var var0 = args.Where(x => x.Length > 0);
            var var1 = var0.Where(x => x.Length < 10);
            var var2 = var1.Select(x => x.ToUpper());
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
            var var2 = args.Where(x => x.Length > 0).Where(x => x.Length < 10).Select(x => x.ToUpper());
        }
    }
}";
            Verify<BlockSyntax, BlockSyntax>(Chain, expected, code);
        }

        [Fact]
        public void ChainConditionalCalls()
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
            var var0 = args?.Where(x => x.Length > 0);
            var var1 = var0.Where(x => x.Length < 10);
            var var2 = var1?.Select(x => x.ToUpper());
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
            var var2 = args?.Where(x => x.Length > 0).Where(x => x.Length < 10)?.Select(x => x.ToUpper());
        }
    }
}";
            Verify<BlockSyntax, BlockSyntax>(Chain, expected, code);
        }

        [Fact]
        public void UnchainCalls()
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
            var var2 = args.Where(x => x.Length > 0).Where(x => x.Length < 10).Select(x => x.ToUpper());
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
            var newVar0 = args.Where(x => x.Length > 0);
            var newVar1 = newVar0.Where(x => x.Length < 10);
            var var2 = newVar1.Select(x => x.ToUpper());
        }
    }
}";
            Verify<StatementSyntax, BlockSyntax>(CallsUnchainer.TryGetAction, expected, code);
        }

        [Fact]
        public void UnchainConditionalCalls1()
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
            var var2 = args?.Where(x => x.Length > 0).Where(x => x.Length < 10)?.Select(x => x.ToUpper());
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
            var newVar0 = args?.Where(x => x.Length > 0);
            var newVar1 = newVar0.Where(x => x.Length < 10);
            var var2 = newVar1?.Select(x => x.ToUpper());
        }
    }
}";
            Verify<StatementSyntax, BlockSyntax>(CallsUnchainer.TryGetAction, expected, code);
        }

        [Fact]
        public void UnchainConditionalCalls2()
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
            var var2 = args?.Where(x => x.Length > 0)?.Where(x => x.Length < 10)?.Select(x => x.ToUpper());
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
            var newVar0 = args?.Where(x => x.Length > 0);
            var newVar1 = newVar0?.Where(x => x.Length < 10);
            var var2 = newVar1?.Select(x => x.ToUpper());
        }
    }
}";
            Verify<StatementSyntax, BlockSyntax>(CallsUnchainer.TryGetAction, expected, code);
        }

        [Fact]
        public void UnchainConditionalCalls3()
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
            var var2 = args.Where(x => x.Length > 0).Where(x => x.Length < 10)?.Select(x => x.ToUpper());
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
            var newVar0 = args.Where(x => x.Length > 0);
            var newVar1 = newVar0.Where(x => x.Length < 10);
            var var2 = newVar1?.Select(x => x.ToUpper());
        }
    }
}";
            Verify<StatementSyntax, BlockSyntax>(CallsUnchainer.TryGetAction, expected, code);
        }

        [Fact]
        public void UnchainConditionalCalls4()
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
            var var2 = args.Where(x => x.Length > 0)?.Where(x => x.Length < 10)?.Select(x => x.ToUpper());
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
            var newVar0 = args.Where(x => x.Length > 0);
            var newVar1 = newVar0?.Where(x => x.Length < 10);
            var var2 = newVar1?.Select(x => x.ToUpper());
        }
    }
}";
            Verify<StatementSyntax, BlockSyntax>(CallsUnchainer.TryGetAction, expected, code);
        }

        [Fact]
        public void UnchainConditionalCalls5()
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
            var var2 = args?.Where(x => x.Length > 0)?.Where(x => x.Length < 10).Select(x => x.ToUpper());
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
            var newVar0 = args?.Where(x => x.Length > 0);
            var newVar1 = newVar0?.Where(x => x.Length < 10);
            var var2 = newVar1.Select(x => x.ToUpper());
        }
    }
}";
            Verify<StatementSyntax, BlockSyntax>(CallsUnchainer.TryGetAction, expected, code);
        }
    }
}
