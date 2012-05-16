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
public class IsIdempotentTest {
    [TestMethod()]
    public void SimpleAssign() {
        var tree = @"
            void f(int x) {
                x = 1;
            }".ParseFunctionTreeFromString();
        var statement = tree.TestGetParsedFunctionStatements().Single();
        Assert.AreEqual(TentativeBool.True, statement.IsIdempotent(tree.GetTestSemanticModel()));
    }
    [TestMethod()]
    public void OpAssign() {
        var tree = @"
            void f(int x) {
                x += 1;
                x -= 1;
                x *= 1;
                x /= 1;
                x &= 1;
                x %= 1;
                x |= 1;
                x ^= 1;
            }".ParseFunctionTreeFromString();
        foreach (var statement in tree.TestGetParsedFunctionStatements())
            Assert.AreEqual(TentativeBool.ProbablyFalse, statement.IsIdempotent(tree.GetTestSemanticModel()));
    }
    [TestMethod()]
    public void Drain() {
        var tree = @"
            void f(int x1, int x2) {
                {
                    x1 = x2;
                    x2 = 0;
                }
            }".ParseFunctionTreeFromString();
        var statement = tree.TestGetParsedFunctionStatements().Single();
        Assert.IsTrue(!statement.IsIdempotent(tree.GetTestSemanticModel()).IsProbablyTrue);
    }
    [TestMethod()]
    public void MultiAssign() {
        var tree = @"
            void f(int x1, int x2) {
                {
                    x1 = 0;
                    x2 = 0;
                }
            }".ParseFunctionTreeFromString();
        var statement = tree.TestGetParsedFunctionStatements().Single();
        Assert.IsTrue(statement.IsIdempotent(tree.GetTestSemanticModel()).IsProbablyTrue);
    }
}
