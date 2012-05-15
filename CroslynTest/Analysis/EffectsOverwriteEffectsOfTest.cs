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
public class EffectsOverwriteEffectsOfTest {
    [TestMethod()]
    public void InitAssignSameYes() {
        var tree = @"
            void f() {
                int a = 0;
                a = 1;
            }".ParseFunctionTreeFromString();
        var statements = tree.TestGetParsedFunctionStatements();
        Assert.IsTrue(statements[1].EffectsOverwriteEffectsOf(statements[0], tree.GetTestSemanticModel()) == true);
    }
    [TestMethod()]
    public void AssignSameYes() {
        var tree = @"
            void f(int a) {
                a = 0;
                a = 1;
            }".ParseFunctionTreeFromString();
        var statements = tree.TestGetParsedFunctionStatements();
        Assert.IsTrue(statements[1].EffectsOverwriteEffectsOf(statements[0], tree.GetTestSemanticModel()) == true);
    }
    [TestMethod()]
    public void AssignDifNo() {
        var tree = @"
            void f(int a, int b) {
                a = 0;
                b = 1;
            }".ParseFunctionTreeFromString();
        var statements = tree.TestGetParsedFunctionStatements();
        Assert.IsTrue(statements[1].EffectsOverwriteEffectsOf(statements[0], tree.GetTestSemanticModel()) != true);
    }
    [TestMethod()]
    public void AssignSubsetNotYes() {
        var tree = @"
            void f(int a, int b) {
                a = b = 0;
                a = 1;
            }".ParseFunctionTreeFromString();
        var statements = tree.TestGetParsedFunctionStatements();
        Assert.IsTrue(statements[1].EffectsOverwriteEffectsOf(statements[0], tree.GetTestSemanticModel()) != true);
    }
    [TestMethod()]
    public void InitAssignSubsetNotYes() {
        var tree = @"
            void f() {
                int a = 0, b = 0;
                a = 1;
            }".ParseFunctionTreeFromString();
        var statements = tree.TestGetParsedFunctionStatements();
        Assert.IsTrue(statements[1].EffectsOverwriteEffectsOf(statements[0], tree.GetTestSemanticModel()) != true);
    }
    [TestMethod()]
    public void AssignEffectUnknown() {
        var tree = @"
            void f(int a, Func<int> x) {
                a = x();
                a = 1;
            }".ParseFunctionTreeFromString();
        var statements = tree.TestGetParsedFunctionStatements();
        Assert.IsTrue(statements[1].EffectsOverwriteEffectsOf(statements[0], tree.GetTestSemanticModel()) == null);
    }
    [TestMethod()]
    public void JumpAssignNo() {
        var tree = @"
            void f(int a, Func<int> x) {
                return;
                a = 1;
            }".ParseFunctionTreeFromString();
        var statements = tree.TestGetParsedFunctionStatements();
        Assert.IsTrue(statements[1].EffectsOverwriteEffectsOf(statements[0], tree.GetTestSemanticModel()) == false);
    }
}
