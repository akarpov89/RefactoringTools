using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.MSBuild;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RefactoringTools
{
    public static class SyntaxNodeExtensions
    {
        private static readonly Lazy<MSBuildWorkspace> LazyDefaultWorkspace = new Lazy<MSBuildWorkspace>(() => MSBuildWorkspace.Create());

        public static TSyntaxNode TryFindParentWithinStatement<TSyntaxNode>(this SyntaxNode node, SyntaxKind kind)
            where TSyntaxNode : SyntaxNode
        {
            do
            {
                node = node.Parent;

                switch (node.CSharpKind())
                {
                    case SyntaxKind.ExpressionStatement:
                    case SyntaxKind.LabeledStatement:
                    case SyntaxKind.GotoStatement:
                    case SyntaxKind.GotoCaseStatement:
                    case SyntaxKind.GotoDefaultStatement:
                    case SyntaxKind.ReturnStatement:
                    case SyntaxKind.YieldReturnStatement:
                    case SyntaxKind.ThrowStatement:
                    case SyntaxKind.WhileStatement:
                    case SyntaxKind.DoStatement:
                    case SyntaxKind.ForStatement:
                    case SyntaxKind.ForEachStatement:
                    case SyntaxKind.UsingStatement:
                    case SyntaxKind.FixedStatement:
                    case SyntaxKind.CheckedStatement:
                    case SyntaxKind.UncheckedStatement:
                    case SyntaxKind.UnsafeStatement:
                    case SyntaxKind.LockStatement:
                    case SyntaxKind.IfStatement:
                    case SyntaxKind.SwitchStatement:
                    case SyntaxKind.TryStatement:
                    case SyntaxKind.MethodDeclaration:
                    case SyntaxKind.ClassDeclaration:
                    case SyntaxKind.NamespaceDeclaration:
                        return null;

                    default:
                        break;
                }

            } while (!node.IsKind(kind));

            return (TSyntaxNode)node;
        }

        public static SyntaxNode Format(this SyntaxNode node)
        {
            return Formatter.Format(node, LazyDefaultWorkspace.Value);
        }

        
    }
}
