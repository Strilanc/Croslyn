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
public class HasSideEffectsTest {
    [TestMethod()]
    public void InvokeFunc_Unknown() {
        var tree = @"
            void f(Func<int> x) {
                x();
            }".ParseFunctionTreeFromString();
        var statement = tree.TestGetParsedFunctionStatements().Single();
        Assert.AreEqual(TentativeBool.Unknown, statement.HasSideEffects(tree.GetTestSemanticModel()));
    }
    [TestMethod()]
    public void AccessMember_ProbablyFalse() {
        var tree = @"
            void f(dynamic x) {
                x.member;
            }".ParseFunctionTreeFromString();
        var statement = tree.TestGetParsedFunctionStatements().Single();
        Assert.AreEqual(TentativeBool.ProbablyFalse, statement.HasSideEffects(tree.GetTestSemanticModel()));
    }
    [TestMethod()]
    public void EmptyStatement_False() {
        var tree = @"
            void f() {
                ;
            }".ParseFunctionTreeFromString();
        var statement = tree.TestGetParsedFunctionStatements().Single();
        Assert.AreEqual(TentativeBool.False, statement.HasSideEffects(tree.GetTestSemanticModel()));
    }
    [TestMethod()]
    public void EmptyBlock_False() {
        var tree = @"
            void f() {
                {}
            }".ParseFunctionTreeFromString();
        var statement = tree.TestGetParsedFunctionStatements().Single();
        Assert.AreEqual(TentativeBool.False, statement.HasSideEffects(tree.GetTestSemanticModel()));
    }
    [TestMethod()]
    public void SafeOperators_ProbablyFalse() {
        var tree = @"
            void f(dynamic x) {
                ~!-x + x - x / x * x % x & x && x || x | x ^ x == x != x > x >= x < x <= x ?? x << x >> x;
            }".ParseFunctionTreeFromString();
        var statement = tree.TestGetParsedFunctionStatements().Single();
        Assert.AreEqual(TentativeBool.ProbablyFalse, statement.HasSideEffects(tree.GetTestSemanticModel()));
    }
    [TestMethod()]
    public void UnsafeOperators_True() {
        var tree = @"
            void f(dynamic x) {
                x--;
                x++;
                ++x;
                --x;
                x += 1;
                x -= 1;
                x *= 1;
                x /= 1;
                x *= 1;
                x %= 1;
                x &= 1;
                x |= 1;
                x ^= 1;
                x <<= 1;
                x >>= 1;
                x = 1;
            }".ParseFunctionTreeFromString();
        var model = tree.GetTestSemanticModel();
        foreach (var statement in tree.TestGetParsedFunctionStatements())
            Assert.AreEqual(TentativeBool.True, statement.HasSideEffects(model));
    }
    [TestMethod()]
    public void PreIncrement_True() {
        var tree = @"
            void f(dynamic x) {
                ++x;
            }".ParseFunctionTreeFromString();
        var statement = tree.TestGetParsedFunctionStatements().Single();
        Assert.AreEqual(TentativeBool.True, statement.HasSideEffects(tree.GetTestSemanticModel()));
    }
    [TestMethod()]
    public void Literal_False() {
        var tree = @"
            void f() {
                1;
            }".ParseFunctionTreeFromString();
        var statement = tree.TestGetParsedFunctionStatements().Single();
        Assert.AreEqual(TentativeBool.False, statement.HasSideEffects(tree.GetTestSemanticModel()));
    }
    [TestMethod()]
    public void Default_False() {
        var tree = @"
            void f() {
                default(bool);
            }".ParseFunctionTreeFromString();
        var statement = tree.TestGetParsedFunctionStatements().Single();
        Assert.AreEqual(TentativeBool.False, statement.HasSideEffects(tree.GetTestSemanticModel()));
    }
    [TestMethod()]
    public void Brackets_Pass() {
        var tree = @"
            void f() {
                (1);
            }".ParseFunctionTreeFromString();
        var statement = tree.TestGetParsedFunctionStatements().Single();
        Assert.AreEqual(TentativeBool.False, statement.HasSideEffects(tree.GetTestSemanticModel()));
        var tree2 = @"
            void f(dynamic x) {
                (x++);
            }".ParseFunctionTreeFromString();
        var statement2 = tree2.TestGetParsedFunctionStatements().Single();
        Assert.AreEqual(TentativeBool.True, statement2.HasSideEffects(tree2.GetTestSemanticModel()));
    }
}
