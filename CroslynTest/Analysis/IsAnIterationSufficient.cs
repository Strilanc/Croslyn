﻿using Croslyn.CodeIssues;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using Roslyn.Compilers.CSharp;
using Roslyn.Compilers.Common;
using System.Threading;
using System.Collections.Generic;
using Roslyn.Compilers;

[TestClass()]
public class IsAnIterationSufficient {
    [TestMethod()]
    public void AssignBreak() {
        var tree = @"
            void f(int[] c, int x) {
                foreach (var i in c) {
                    x = i;
                    break;
                }
            }".ParseFunctionTreeFromString();
        var loop = (ForEachStatementSyntax)tree.TestGetParsedFunctionStatements().Single();
        var model = tree.GetTestSemanticModel();
        Assert.IsTrue(loop.IsAnyIterationSufficient(model, Assumptions.All) != true);
        Assert.IsTrue(loop.IsFirstIterationSufficient(model, Assumptions.All) == true);
        Assert.IsTrue(loop.IsLastIterationSufficient(model, Assumptions.All) != true);
    }
    [TestMethod()]
    public void Throw() {
        var tree = @"
            void f(int[] c, int x) {
                foreach (var i in c) {
                    throw new System.Exception();
                }
            }".ParseFunctionTreeFromString();
        var loop = (ForEachStatementSyntax)tree.TestGetParsedFunctionStatements().Single();
        var model = tree.GetTestSemanticModel();
        Assert.IsTrue(loop.IsAnyIterationSufficient(model, Assumptions.All) == true);
        Assert.IsTrue(loop.IsFirstIterationSufficient(model, Assumptions.All) == true);
        Assert.IsTrue(loop.IsLastIterationSufficient(model, Assumptions.All) == true);
    }
    [TestMethod()]
    public void MayReturn() {
        var tree = @"
            int f(int[] c, int x, bool b) {
                foreach (var i in c) {
                    if (i == 0) return 5;
                    x = 0;
                }
            }".ParseFunctionTreeFromString();
        var loop = (ForEachStatementSyntax)tree.TestGetParsedFunctionStatements().Single();
        var model = tree.GetTestSemanticModel();
        Assert.IsTrue(loop.IsAnyIterationSufficient(model, Assumptions.All) != true);
        Assert.IsTrue(loop.IsFirstIterationSufficient(model, Assumptions.All) != true);
        Assert.IsTrue(loop.IsLastIterationSufficient(model, Assumptions.All) != true);
    }
    [TestMethod()]
    public void RotatingAssign() {
        var tree = @"
            void f(int[] c, int x1, int x2) {
                foreach (var i in c) {
                    int t = x1;
                    x1 = x2;
                    x2 = t;
                }
            }".ParseFunctionTreeFromString();
        var loop = (ForEachStatementSyntax)tree.TestGetParsedFunctionStatements().Single();
        var model = tree.GetTestSemanticModel();
        Assert.IsTrue(loop.IsAnyIterationSufficient(model, Assumptions.All) != true);
        Assert.IsTrue(loop.IsFirstIterationSufficient(model, Assumptions.All) != true);
        Assert.IsTrue(loop.IsLastIterationSufficient(model, Assumptions.All) != true);
    }
    [TestMethod()]
    public void AssignContinue() {
        var tree = @"
            void f(int[] c, int x) {
                foreach (var i in c) {
                    x = i;
                    continue;
                }
            }".ParseFunctionTreeFromString();
        var loop = (ForEachStatementSyntax)tree.TestGetParsedFunctionStatements().Single();
        var model = tree.GetTestSemanticModel();
        Assert.IsTrue(loop.IsAnyIterationSufficient(model, Assumptions.All) != true);
        Assert.IsTrue(loop.IsFirstIterationSufficient(model, Assumptions.All) != true);
        Assert.IsTrue(loop.IsLastIterationSufficient(model, Assumptions.All) == true);
    }
    [TestMethod()]
    public void Assign() {
        var tree = @"
            void f(int[] c, int x) {
                foreach (var i in c) {
                    x = i;
                }
            }".ParseFunctionTreeFromString();
        var loop = (ForEachStatementSyntax)tree.TestGetParsedFunctionStatements().Single();
        var model = tree.GetTestSemanticModel();
        Assert.IsTrue(loop.IsAnyIterationSufficient(model, Assumptions.All) != true);
        Assert.IsTrue(loop.IsFirstIterationSufficient(model, Assumptions.All) != true);
        Assert.IsTrue(loop.IsLastIterationSufficient(model, Assumptions.All) == true);
    }
    [TestMethod()]
    public void AssignConst() {
        var tree = @"
            void f(int[] c, int x) {
                foreach (var i in c) {
                    x = 2;
                }
            }".ParseFunctionTreeFromString();
        var loop = (ForEachStatementSyntax)tree.TestGetParsedFunctionStatements().Single();
        var model = tree.GetTestSemanticModel();
        Assert.IsTrue(loop.IsAnyIterationSufficient(model, Assumptions.All) == true);
        Assert.IsTrue(loop.IsFirstIterationSufficient(model, Assumptions.All) == true);
        Assert.IsTrue(loop.IsLastIterationSufficient(model, Assumptions.All) == true);
    }
}
