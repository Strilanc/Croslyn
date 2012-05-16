using Croslyn.CodeIssues;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using Roslyn.Compilers.CSharp;
using Roslyn.Compilers.Common;
using System.Threading;
using System.Collections.Generic;
using Roslyn.Compilers;

[TestClass()]
public class IsConstTest {
    [TestMethod()]
    public void Literal_True() {
        var tree = @"
            void f(int x) {
                x = 1;
                x = 2;
                x = default(object);
                x = null;
                x = 0.0;
                x = true;
            }".ParseFunctionTreeFromString();
        foreach (var statement in tree.TestGetParsedFunctionStatements()) {
            var rhs = ((statement as ExpressionStatementSyntax).Expression as BinaryExpressionSyntax).Right;
            Assert.AreEqual(TentativeBool.True, rhs.IsConst(tree.GetTestSemanticModel()));
        }
    }
    [TestMethod()]
    public void Default_True() {
        var tree = @"
            void f(int x) {
                x = default(object);
                x = default(bool);
            }".ParseFunctionTreeFromString();
        foreach (var statement in tree.TestGetParsedFunctionStatements()) {
            var rhs = ((statement as ExpressionStatementSyntax).Expression as BinaryExpressionSyntax).Right;
            Assert.AreEqual(TentativeBool.True, rhs.IsConst(tree.GetTestSemanticModel()));
        }
    }
    [TestMethod()]
    public void Var_NotTrue() {
        var tree = @"
            void f(int x, int p) {
                var v = 0;
                x = v;
                x = p;
            }".ParseFunctionTreeFromString();
        foreach (var statement in tree.TestGetParsedFunctionStatements().Skip(1)) {
            var rhs = ((statement as ExpressionStatementSyntax).Expression as BinaryExpressionSyntax).Right;
            Assert.IsTrue(rhs.IsConst(tree.GetTestSemanticModel()) != true);
        }
    }
    [TestMethod()]
    public void SafeOp_PassGood() {
        var tree = @"
            void f(int x, int p) {
                x = 0 + 1 - 2 * 3 / 4 % 5 >> 6 << 7 ^ 8;
            }".ParseFunctionTreeFromString();
        foreach (var statement in tree.TestGetParsedFunctionStatements()) {
            var rhs = ((statement as ExpressionStatementSyntax).Expression as BinaryExpressionSyntax).Right;
            Assert.IsTrue(rhs.IsConst(tree.GetTestSemanticModel()) == true);
        }
    }
    [TestMethod()]
    public void SafeOp_PassBad() {
        var tree = @"
            void f(int x, int p) {
                x = p - 1;
            }".ParseFunctionTreeFromString();
        foreach (var statement in tree.TestGetParsedFunctionStatements()) {
            var rhs = ((statement as ExpressionStatementSyntax).Expression as BinaryExpressionSyntax).Right;
            Assert.IsTrue(rhs.IsConst(tree.GetTestSemanticModel()) != true);
        }
    }
    [TestMethod()]
    public void Paren_PassGood() {
        var tree = @"
            void f(int x, int p) {
                x = (0);
                x = (true);
                x = ((((true))));
            }".ParseFunctionTreeFromString();
        foreach (var statement in tree.TestGetParsedFunctionStatements()) {
            var rhs = ((statement as ExpressionStatementSyntax).Expression as BinaryExpressionSyntax).Right;
            Assert.IsTrue(rhs.IsConst(tree.GetTestSemanticModel()) == true);
        }
    }
    [TestMethod()]
    public void Paren_PassBad() {
        var tree = @"
            void f(int x, int p) {
                x = (p);
            }".ParseFunctionTreeFromString();
        foreach (var statement in tree.TestGetParsedFunctionStatements()) {
            var rhs = ((statement as ExpressionStatementSyntax).Expression as BinaryExpressionSyntax).Right;
            Assert.IsTrue(rhs.IsConst(tree.GetTestSemanticModel()) != true);
        }
    }
}
