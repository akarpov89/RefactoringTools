using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
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

        public static TSyntaxNode TryFindParentWithinStatement<TSyntaxNode>(this SyntaxNode node, SyntaxKind kind, Func<TSyntaxNode, bool> predicate = null)
            where TSyntaxNode : SyntaxNode
        {
            do
            {
                node = node.Parent;

                if (node.IsStatement() || node.IsMethodOrClassOrNamespace())
                    return null;

                if (node.IsKind(kind))
                {
                    var temp = (TSyntaxNode)node;
                    if (predicate != null)
                    {
                        if (predicate(temp))
                        {
                            return temp;
                        }
                    }
                    else
                    {
                        return temp;
                    }
                }

            } while (true);            
        }

        public static TSyntaxNode TryFindOuterMostParentWithinStatement<TSyntaxNode>(this SyntaxNode node, SyntaxKind kind, Func<TSyntaxNode, bool> predicate = null)
            where TSyntaxNode : SyntaxNode
        {
            TSyntaxNode lastMatched = null;

            do
            {
                node = node.Parent;

                if (node.IsStatement() || node.IsMethodOrClassOrNamespace())
                    return lastMatched;

                if (node.IsKind(kind))
                {
                    var temp = (TSyntaxNode)node;

                    if (predicate != null)
                    {
                        if (predicate(temp))
                        {
                            lastMatched = temp;
                        }
                    }
                    else
                    {
                        lastMatched = temp;
                    }
                }

            } while (true);                                    
        }

        public static BlockSyntax TryFindParentBlock(this SyntaxNode node)
        {
            do
            {
                if (node.IsMethodOrClassOrNamespace())
                    return null;

                node = node.Parent;

            } while (!node.IsKind(SyntaxKind.Block));

            return (BlockSyntax)node;
        }

        public static StatementSyntax TryFindParentStatement(this SyntaxNode node)
        {
            do
            {
                if (node.IsMethodOrClassOrNamespace())
                    return null;

                node = node.Parent;

            } while (!node.IsStatement());

            return (StatementSyntax)node;
        }

        private static bool IsStatement(this SyntaxNode node)
        {
            switch (node.CSharpKind())
            {
                case SyntaxKind.LocalDeclarationStatement:
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
                    return true;
                default:
                    return false;
            }
        }

        private static bool IsMethodOrClassOrNamespace(this SyntaxNode node)
        {
            return node.IsKind(SyntaxKind.MethodDeclaration) || node.IsKind(SyntaxKind.ClassDeclaration) || node.IsKind(SyntaxKind.NamespaceDeclaration);
        }

        public static T Format<T>(this T node) where T : SyntaxNode
        {
            return node.WithAdditionalAnnotations(Formatter.Annotation);
        }
    }
}
