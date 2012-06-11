using Croslyn.CodeIssues;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using Roslyn.Compilers.CSharp;
using Roslyn.Compilers.Common;
using System.Threading;
using System.Collections.Generic;
using Roslyn.Compilers;
using Strilbrary.Collections;

[TestClass()]
public class OverexposedLocalTest {
    private void AssertOptimizes(string pars, string body, params string[][] newBodies) {
        var tree1 = ("void f(" + pars + ") { "+body + " }").ParseFunctionTreeFromString();
        var model = tree1.GetTestSemanticModel();
        var body1 = tree1.TestGetParsedFunctionBody();
        var declarationStatements = body1.Statements.OfType<LocalDeclarationStatementSyntax>().ToArray();
        Assert.IsTrue(declarationStatements.Length == newBodies.Length);
        for (var i = 0; i < declarationStatements.Length; i++) {
            var r = OverexposedLocal.GetSimplifications(declarationStatements[i], model, Assumptions.All).ToArray();
            Assert.IsTrue(newBodies[i].Length == r.Length);
            for (var j = 0; j < newBodies[i].Length; j++) {
                Assert.IsTrue(r[j].OldNode == body1);
                var tree2 = ("void f(" + pars + ") { " + newBodies[i][j] + " }").ParseFunctionTreeFromString();
                var body2 = tree2.TestGetParsedFunctionBody();
                r[j].NewNode.AssertSameSyntax(body2);
            }
        }
    }

    [TestMethod()]
    public void FilterTest() {
        AssertOptimizes(
            "Action a",
            "int c; a(); c = 0;",
            new[] { "a(); int c; c = 0;" });
        AssertOptimizes(
            "bool b",
            "int c; if (b) { c = 0; }",
            new[] { "if (b) { int c; c = 0; }" });
        AssertOptimizes(
            "bool b",
            "int c; if (b) {} else { c = 0; }",
            new[] { "if (b) {} else { int c; c = 0; }" });
        AssertOptimizes(
            "int f",
            "int c = f; f = 2; c += 1;",
            new String[0]);
        AssertOptimizes(
            "int f",
            "int c = 0; f = 2; c += 1;",
            new[] { "f = 2; int c = 0; c += 1;" });
        AssertOptimizes(
            "Action a",
            "a() int c; c = 0;",
            new String[0]);
        AssertOptimizes(
            "Action a",
            "int c1, c2; a(); c1 = c2 = 0;",
            new[] {"int c2; a(); int c1; c1 = c2 = 0;",
                   "int c1; a(); int c2; c1 = c2 = 0;"});
        AssertOptimizes(
            "Action a",
            "int c1; int c2; a(); c1 = c2 = 0;",
            new[] { "int c2; a(); int c1; c1 = c2 = 0;" },
            new[] { "int c1; a(); int c2; c1 = c2 = 0;" });

        AssertOptimizes(
            "",
            "int c; { c = 1; }",
            new[] { "{ int c; c = 1; }" });
        AssertOptimizes(
            "Action a",
            "int c; { a(); c = 1; }",
            new[] { "{ a(); int c; c = 1; }" });
        AssertOptimizes(
            "Action a",
            "int c; { a(); { a(); a(); c = 1; a(); } }",
            new[] { "{ a(); { a(); a(); int c; c = 1; a(); } }" });

        AssertOptimizes(
            "Action a",
            "int c1; a(); int c2; c1 = c2 = 0;",
            new[] { "a(); int c2; int c1; c1 = c2 = 0;" },
            new String[0]);
        AssertOptimizes(
            "Action a",
            "a(); int c1; int c2; c1 = c2 = 0;",
            new String[0],
            new String[0]);
        AssertOptimizes(
            "Func<int> f",
            "int c = f(); { a(); { a(); a(); c = 1; a(); } }",
            new String[0]);
        AssertOptimizes(
            "Action a",
            "int c = 2; { a(); { a(); a(); c = 1; a(); } }",
            new[] { "{ a(); { a(); a(); int c = 2; c = 1; a(); } }" });
        AssertOptimizes(
            "Action a, int f",
            "int c = f; { a(); { a(); a(); c = 1; a(); } }",
            new[] {"{ int c = f; a(); { a(); a(); c = 1; a(); } }"});
        
        AssertOptimizes(
            "",
            "var L = new List<int>(); ; L.Add(1); }",
            new[] { "; var L = new List<int>(); L.Add(1); }" });
    }
}
