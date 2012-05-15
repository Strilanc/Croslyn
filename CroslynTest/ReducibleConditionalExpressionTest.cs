using Croslyn.CodeIssues;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using Roslyn.Compilers.CSharp;
using Roslyn.Compilers.Common;
using System.Threading;
using System.Collections.Generic;
using Roslyn.Compilers;

namespace CroslynTest {
    [TestClass()]
    public class ReducibleConditionalExpressionTest {
        private void AssertSameSyntax(SyntaxToken n1, SyntaxToken n2) {
            Assert.IsTrue(n1.Kind == n2.Kind);
        }
        private void AssertSameSyntax(SyntaxNode n1, SyntaxNode n2) {
            Assert.IsTrue(n1.Kind == n2.Kind);
            if (n1 is SimpleNameSyntax || n1 is IdentifierNameSyntax || n1 is LiteralExpressionSyntax) {
                Assert.AreEqual(n1.WithoutTrivia().ToString(), n2.WithoutTrivia().ToString());
            } else if (n1 is MemberAccessExpressionSyntax) {
                var m1 = (MemberAccessExpressionSyntax)n1;
                var m2 = (MemberAccessExpressionSyntax)n2;
                AssertSameSyntax(m1.Expression, m2.Expression);
                Assert.IsTrue(m1.Name == m2.Name);
            } else if (n1 is InvocationExpressionSyntax) {
                var m1 = (InvocationExpressionSyntax)n1;
                var m2 = (InvocationExpressionSyntax)n2;
                AssertSameSyntax(m1.Expression, m2.Expression);
                Assert.IsTrue(m1.ArgumentList.Arguments.Count == m2.ArgumentList.Arguments.Count);
                for (var i = 0; i < m1.ArgumentList.Arguments.Count; i++) {
                    AssertSameSyntax(m1.ArgumentList.Arguments[i], m2.ArgumentList.Arguments[i]);
                }
            } else if (n1 is BinaryExpressionSyntax) {
                var m1 = (BinaryExpressionSyntax)n1;
                var m2 = (BinaryExpressionSyntax)n2;
                AssertSameSyntax(m1.Left, m2.Left);
                AssertSameSyntax(m1.Right, m2.Right);
                AssertSameSyntax(m1.OperatorToken, m2.OperatorToken);
            } else if (n1 is ConditionalExpressionSyntax) {
                var m1 = (ConditionalExpressionSyntax)n1;
                var m2 = (ConditionalExpressionSyntax)n2;
                AssertSameSyntax(m1.Condition, m2.Condition);
                AssertSameSyntax(m1.WhenTrue, m2.WhenTrue);
                AssertSameSyntax(m1.WhenFalse, m2.WhenFalse);
            } else if (n1 is PrefixUnaryExpressionSyntax) {
                var m1 = (PrefixUnaryExpressionSyntax)n1;
                var m2 = (PrefixUnaryExpressionSyntax)n2;
                AssertSameSyntax(m1.Operand, m2.Operand);
                AssertSameSyntax(m1.OperatorToken, m2.OperatorToken);
            } else if (n1 is ParenthesizedExpressionSyntax) {
                var m1 = (ParenthesizedExpressionSyntax)n1;
                var m2 = (ParenthesizedExpressionSyntax)n2;
                AssertSameSyntax(m1.Expression, m2.Expression);
            } else {
                throw new NotImplementedException();
            }
        }
        private ExpressionSyntax ParseExpressionFromString(String s) {
            var p = SyntaxTree.ParseCompilationUnit(s, options: new ParseOptions(kind: SourceCodeKind.Interactive));
            var compUnit = (CompilationUnitSyntax)p.Root;
            var globalStatement = (GlobalStatementSyntax)compUnit.ChildNodes().Single();
            var expStatement = (ExpressionStatementSyntax)globalStatement.ChildNodes().Single();
            return expStatement.Expression;
        }
        private SyntaxTree ParseFunctionTreeFromString(String s) {
            var tree = SyntaxTree.ParseCompilationUnit(s, options: new ParseOptions(kind: SourceCodeKind.Interactive));
            var compUnit = (CompilationUnitSyntax)tree.Root;
            var method = (MethodDeclarationSyntax)compUnit.ChildNodes().Single();
            return tree;
        }
        private ISemanticModel GetModel(SyntaxTree tree) {
            return Compilation.Create("temp")
                   .AddSyntaxTrees(tree)
                   .AddReferences(new AssemblyFileReference(typeof(object).Assembly.Location))
                   .GetSemanticModel(tree);
        }

        [TestMethod()]
        public void OppositeSimplificationsTest() {
            var tree = ParseFunctionTreeFromString(@"
                bool f(bool b1, bool b2) { 
                    return b1 ? b2 : !b2;
                }");
            var conditional = tree.Root.DescendentNodes().OfType<ConditionalExpressionSyntax>().Single();
            var replacement = ReducibleConditionalExpression.GetSimplifications(conditional, GetModel(tree)).Single();
            Assert.IsTrue(replacement.OldNode == conditional);
            AssertSameSyntax(replacement.NewNode, ParseExpressionFromString("b1 == b2"));
        }
        [TestMethod()]
        public void EquivalentSimplificationsTest() {
            var tree = ParseFunctionTreeFromString(@"
                bool f(bool b1, bool b2) { 
                    return b1 ? b2 : b2;
                }");
            var conditional = tree.Root.DescendentNodes().OfType<ConditionalExpressionSyntax>().Single();
            var replacement = ReducibleConditionalExpression.GetSimplifications(conditional, GetModel(tree)).Single();
            Assert.IsTrue(replacement.OldNode == conditional);
            AssertSameSyntax(replacement.NewNode, ParseExpressionFromString("b2"));
        }
        [TestMethod()]
        public void UnsafeEquivalentSimplificationsTest() {
            var tree = ParseFunctionTreeFromString(@"
                bool f(Func<bool> b1, bool b2) { 
                    return b1() ? b2 : b2;
                }");
            var conditional = tree.Root.DescendentNodes().OfType<ConditionalExpressionSyntax>().Single();
            var replacements = ReducibleConditionalExpression.GetSimplifications(conditional, GetModel(tree)).ToArray();
            Assert.IsTrue(replacements.Length == 2);
            Assert.IsTrue(replacements[0].OldNode == conditional);
            Assert.IsTrue(replacements[1].OldNode == conditional);
            AssertSameSyntax(replacements[0].NewNode, ParseExpressionFromString("b2"));
            AssertSameSyntax(replacements[1].NewNode, ParseExpressionFromString("(b1() && false) || b2"));
        }
    }
}
