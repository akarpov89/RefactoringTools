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

        [Fact]
        public void UnchainConditionalCalls6()
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
            var var2 = args?[0].Select(x => x.ToString())?.Where(x => true);
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
            var newVar0 = args?[0].Select(x => x.ToString());            
            var var2 = newVar0?.Where(x => true);
        }
    }
}";
            Verify<StatementSyntax, BlockSyntax>(CallsUnchainer.TryGetAction, expected, code);
        }

        [Fact]
        public void UnchainConditionalCalls7()
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
            var var2 = args?[0]?.Select(x => x.ToString())?.Where(x => true);
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
            var newVar0 = args?[0]?.Select(x => x.ToString());            
            var var2 = newVar0?.Where(x => true);
        }
    }
}";
            Verify<StatementSyntax, BlockSyntax>(CallsUnchainer.TryGetAction, expected, code);
        }

        [Fact]
        public void UnchainConditionalCalls8()
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
            var var2 = args?.Select(x => x.ToString())?.Where(x => true);
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
            var newVar0 = args?.Select(x => x.ToString());
            var var2 = newVar0?.Where(x => true);
        }
    }
}";
            Verify<StatementSyntax, BlockSyntax>(CallsUnchainer.TryGetAction, expected, code);
        }

        [Fact]
        public void UnchainConditionalCalls9()
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
            var var2 = args.Length.ToString().Select(x => x.ToString())?.Where(x => true);
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
            var newVar0 = args.Length.ToString();
            var newVar1 = newVar0.Select(x => x.ToString());
            var var2 = newVar1?.Where(x => true);
        }
    }
}";
            Verify<StatementSyntax, BlockSyntax>(CallsUnchainer.TryGetAction, expected, code);
        }

        [Fact]
        public void UnchainConditionalCalls10()
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
            var var2 = args?.Length?.ToString().Select(x => x.ToString())?.Where(x => true);
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
            var newVar0 = args?.Length?.ToString();
            var newVar1 = newVar0.Select(x => x.ToString());
            var var2 = newVar1?.Where(x => true);
        }
    }
}";
            Verify<StatementSyntax, BlockSyntax>(CallsUnchainer.TryGetAction, expected, code);
        }

        [Fact]
        public void UnchainConditionalCalls11()
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
            var var2 = args.Length?.ToString().Select(x => x.ToString())?.Where(x => true);
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
            var newVar0 = args.Length?.ToString();
            var newVar1 = newVar0.Select(x => x.ToString());
            var var2 = newVar1?.Where(x => true);
        }
    }
}";
            Verify<StatementSyntax, BlockSyntax>(CallsUnchainer.TryGetAction, expected, code);
        }

        [Fact]
        public void UnchainConditionalCalls12()
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
            var var2 = args?.Length.ToString().Select(x => x.ToString())?.Where(x => true);
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
            var newVar0 = args?.Length.ToString();
            var newVar1 = newVar0.Select(x => x.ToString());
            var var2 = newVar1?.Where(x => true);
        }
    }
}";
            Verify<StatementSyntax, BlockSyntax>(CallsUnchainer.TryGetAction, expected, code);
        }

        [Fact]
        public void UnchainConditionalCalls13()
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
            var var2 = args?.Length.Length.ToString().Select(x => x.ToString())?.Where(x => true);
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
            var newVar0 = args?.Length.Length.ToString();
            var newVar1 = newVar0.Select(x => x.ToString());
            var var2 = newVar1?.Where(x => true);
        }
    }
}";
            Verify<StatementSyntax, BlockSyntax>(CallsUnchainer.TryGetAction, expected, code);
        }

        [Fact]
        public void UnchainConditionalCalls14()
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
            var var2 = args?.Xxx?.Yyy.ToString().Select(x => x.ToString())?.Where(x => true);
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
            var newVar0 = args?.Xxx?.Yyy.ToString();
            var newVar1 = newVar0.Select(x => x.ToString());
            var var2 = newVar1?.Where(x => true);
        }
    }
}";
            Verify<StatementSyntax, BlockSyntax>(CallsUnchainer.TryGetAction, expected, code);
        }

        [Fact]
        public void UnchainConditionalCalls15()
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
            var var2 = args.Xxx?.Yyy.ToString().Select(x => x.ToString())?.Where(x => true);
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
            var newVar0 = args.Xxx?.Yyy.ToString();
            var newVar1 = newVar0.Select(x => x.ToString());
            var var2 = newVar1?.Where(x => true);
        }
    }
}";
            Verify<StatementSyntax, BlockSyntax>(CallsUnchainer.TryGetAction, expected, code);
        }

        [Fact]
        public void UnchainConditionalCalls16()
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
            var var2 = args.Xxx?.Yyy?.ToString().Select(x => x.ToString())?.Where(x => true);
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
            var newVar0 = args.Xxx?.Yyy?.ToString();
            var newVar1 = newVar0.Select(x => x.ToString());
            var var2 = newVar1?.Where(x => true);
        }
    }
}";
            Verify<StatementSyntax, BlockSyntax>(CallsUnchainer.TryGetAction, expected, code);
        }

        [Fact]
        public void UnchainConditionalCalls17()
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
            var var2 = args?.Xxx?.Yyy?.ToString().Select(x => x.ToString())?.Where(x => true);
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
            var newVar0 = args?.Xxx?.Yyy?.ToString();
            var newVar1 = newVar0.Select(x => x.ToString());
            var var2 = newVar1?.Where(x => true);
        }
    }
}";
            Verify<StatementSyntax, BlockSyntax>(CallsUnchainer.TryGetAction, expected, code);
        }

        [Fact]
        public void UnchainConditionalCalls18()
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
            var var2 = args?.Xxx?.Yyy.ToString().Select(x => x.ToString())?.Where(x => true);
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
            var newVar0 = args?.Xxx?.Yyy.ToString();
            var newVar1 = newVar0.Select(x => x.ToString());
            var var2 = newVar1?.Where(x => true);
        }
    }
}";
            Verify<StatementSyntax, BlockSyntax>(CallsUnchainer.TryGetAction, expected, code);
        }

        [Fact]
        public void UnchainConditionalCalls19()
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
            var var2 = args?.Xxx?.Yyy.ToString()?.Select(x => x.ToString())?.Where(x => true);
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
            var newVar0 = args?.Xxx?.Yyy.ToString();
            var newVar1 = newVar0?.Select(x => x.ToString());
            var var2 = newVar1?.Where(x => true);
        }
    }
}";
            Verify<StatementSyntax, BlockSyntax>(CallsUnchainer.TryGetAction, expected, code);
        }

        [Fact]
        public void UnchainConditionalCalls20()
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
            var var2 = args?.Xxx.Yyy?.ToString()?.Select(x => x.ToString())?.Where(x => true);
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
            var newVar0 = args?.Xxx.Yyy?.ToString();
            var newVar1 = newVar0?.Select(x => x.ToString());
            var var2 = newVar1?.Where(x => true);
        }
    }
}";
            Verify<StatementSyntax, BlockSyntax>(CallsUnchainer.TryGetAction, expected, code);
        }

        [Fact]
        public void UnchainConditionalCalls21()
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
            var var2 = args?[0]?[1].Xxx.Yyy?.ToString()?.Select(x => x.ToString())?.Where(x => true);
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
            var newVar0 = args?[0]?[1].Xxx.Yyy?.ToString();
            var newVar1 = newVar0?.Select(x => x.ToString());
            var var2 = newVar1?.Where(x => true);
        }
    }
}";
            Verify<StatementSyntax, BlockSyntax>(CallsUnchainer.TryGetAction, expected, code);
        }

        [Fact]
        public void UnchainConditionalCalls22()
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
            var var2 = args[0][1].Xxx.Yyy?.ToString()?.Select(x => x.ToString())?.Where(x => true);
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
            var newVar0 = args[0][1].Xxx.Yyy?.ToString();
            var newVar1 = newVar0?.Select(x => x.ToString());
            var var2 = newVar1?.Where(x => true);
        }
    }
}";
            Verify<StatementSyntax, BlockSyntax>(CallsUnchainer.TryGetAction, expected, code);
        }

        [Fact]
        public void UnchainConditionalCalls23()
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
            var var2 = args[0]?[1].Xxx.Yyy?.ToString()?.Select(x => x.ToString())?.Where(x => true);
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
            var newVar0 = args[0]?[1].Xxx.Yyy?.ToString();
            var newVar1 = newVar0?.Select(x => x.ToString());
            var var2 = newVar1?.Where(x => true);
        }
    }
}";
            Verify<StatementSyntax, BlockSyntax>(CallsUnchainer.TryGetAction, expected, code);
        }

        [Fact]
        public void UnchainConditionalCalls24()
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
            var r = args.Xxx()?.Yyy();
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
            var newVar0 = args.Xxx();
            var r = newVar0?.Yyy();
        }
    }
}";
            Verify<StatementSyntax, BlockSyntax>(CallsUnchainer.TryGetAction, expected, code);
        }

        [Fact]
        public void UnchainConditionalCalls25()
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
            var r = args?.Xxx().Yyy();
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
            var newVar0 = args?.Xxx();
            var r = newVar0.Yyy();
        }
    }
}";
            Verify<StatementSyntax, BlockSyntax>(CallsUnchainer.TryGetAction, expected, code);
        }

        [Fact]
        public void UnchainConditionalCalls26()
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
            var r = args?.Xxx().Ppp.Yyy();
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
            var r = args?.Xxx().Ppp.Yyy();
        }
    }
}";
            Verify<StatementSyntax, BlockSyntax>(CallsUnchainer.TryGetAction, expected, code);
        }

        [Fact]
        public void UnchainConditionalCalls27()
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
            var r = args?.Xxx().Ppp.Yyy()?.Zzz();
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
            var newVar0 = args?.Xxx().Ppp.Yyy();
            var r = newVar0?.Zzz();
        }
    }
}";
            Verify<StatementSyntax, BlockSyntax>(CallsUnchainer.TryGetAction, expected, code);
        }

        [Fact]
        public void UnchainConditionalCalls28()
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
            var r = args?.Xxx().Ppp?.Yyy()?.Zzz();
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
            var newVar0 = args?.Xxx().Ppp?.Yyy();
            var r = newVar0?.Zzz();
        }
    }
}";
            Verify<StatementSyntax, BlockSyntax>(CallsUnchainer.TryGetAction, expected, code);
        }

        [Fact]
        public void UnchainConditionalCalls29()
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
            var r = args?.Xxx()?.Ppp?.Yyy()?.Zzz();
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
            var newVar0 = args?.Xxx()?.Ppp?.Yyy();
            var r = newVar0?.Zzz();
        }
    }
}";
            Verify<StatementSyntax, BlockSyntax>(CallsUnchainer.TryGetAction, expected, code);
        }

        [Fact]
        public void UnchainConditionalCalls30()
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
            var r = args?.Xxx()?.Ppp.Yyy()?.Zzz();
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
            var newVar0 = args?.Xxx()?.Ppp.Yyy();
            var r = newVar0?.Zzz();
        }
    }
}";
            Verify<StatementSyntax, BlockSyntax>(CallsUnchainer.TryGetAction, expected, code);
        }

        [Fact]
        public void UnchainConditionalCalls31()
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
            var r = args?.Xxx()?.Ppp.Rrr.Yyy()?.Zzz();
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
            var newVar0 = args?.Xxx()?.Ppp.Rrr.Yyy();
            var r = newVar0?.Zzz();
        }
    }
}";
            Verify<StatementSyntax, BlockSyntax>(CallsUnchainer.TryGetAction, expected, code);
        }

        [Fact]
        public void UnchainConditionalCalls32()
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
            var r = args?.Xxx()?.Ppp.Rrr?.Yyy()?.Zzz();
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
            var newVar0 = args?.Xxx()?.Ppp.Rrr?.Yyy();
            var r = newVar0?.Zzz();
        }
    }
}";
            Verify<StatementSyntax, BlockSyntax>(CallsUnchainer.TryGetAction, expected, code);
        }

        [Fact]
        public void UnchainConditionalCalls33()
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
            var r = args?.Xxx()?.Ppp?.Rrr?.Yyy()?.Zzz();
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
            var newVar0 = args?.Xxx()?.Ppp?.Rrr?.Yyy();
            var r = newVar0?.Zzz();
        }
    }
}";
            Verify<StatementSyntax, BlockSyntax>(CallsUnchainer.TryGetAction, expected, code);
        }

        [Fact]
        public void UnchainConditionalCalls34()
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
            var r = args?.Xxx()?.Ppp?.Rrr[0].Yyy()?.Zzz();
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
            var newVar0 = args?.Xxx()?.Ppp?.Rrr[0].Yyy();
            var r = newVar0?.Zzz();
        }
    }
}";
            Verify<StatementSyntax, BlockSyntax>(CallsUnchainer.TryGetAction, expected, code);
        }

        [Fact]
        public void UnchainConditionalCalls35()
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
            var r = args?.Xxx()?.Ppp.Rrr?[0].Yyy()?.Zzz();
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
            var newVar0 = args?.Xxx()?.Ppp.Rrr?[0].Yyy();
            var r = newVar0?.Zzz();
        }
    }
}";
            Verify<StatementSyntax, BlockSyntax>(CallsUnchainer.TryGetAction, expected, code);
        }
    }
}
