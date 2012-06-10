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
public class ForEachToAnyTest {
    private void AssertNoOptimization(Assumptions assumptions, string pars, string collection, string body) {
        var tree1 = ("void f(" + pars + ") { foreach (var e in " + collection + ") { " + body + " }").ParseFunctionTreeFromStringUsingStandard();
        var statements1 = (ForEachStatementSyntax)tree1.TestGetParsedFunctionStatements().Single();
        var model = tree1.GetTestSemanticModel();
        Assert.IsTrue(ForEachToAny.GetSimplifications(statements1, model, assumptions).Count() == 0);
    }
    private void AssertOptimizes(Assumptions assumptions, string pars, string collection, string body, string newBody = null) {
        var tree1 = ("void f(" + pars + ") { foreach (var e in " + collection + ") { " + body + " }").ParseFunctionTreeFromStringUsingStandard();
        var tree2 = ("void f(" + pars + ") { if (" + collection + ".Any()) { " + (newBody ?? body) + " }").ParseFunctionTreeFromStringUsingStandard();
        var statements1 = (ForEachStatementSyntax)tree1.TestGetParsedFunctionStatements().Single();
        var statements2 = (IfStatementSyntax)tree2.TestGetParsedFunctionStatements().Single();
        var model = tree1.GetTestSemanticModel();
        TestUtil.AssertSameSyntax(ForEachToAny.GetSimplifications(statements1, model, assumptions).Single().NewNode, statements2);
    }

    [TestMethod()]
    public void ForEachToIfAnyTest() {
        AssertOptimizes(
            Assumptions.All,
            "IEnumerable<int> c, bool v",
            "c",
            "v = true;");
        AssertOptimizes(
            Assumptions.All,
            "IEnumerable<int> c, bool v",
            "c",
            "v = true; break;",
            "v = true;");
        AssertOptimizes(
            Assumptions.All,
            "IEnumerable<int> c, bool v",
            "c",
            "v = true; continue;",
            "v = true;");
        AssertOptimizes(
            Assumptions.All,
            "IEnumerable<int> c, bool v, bool m",
            "c",
            "v = m;");

        AssertNoOptimization(
            Assumptions.None,
            "IEnumerable<int> c, bool v",
            "c",
            "v = true;");
        AssertNoOptimization(
            Assumptions.None,
            "IEnumerable<int> c, bool v",
            "c",
            "v ^= true;");
        AssertNoOptimization(
            Assumptions.All,
            "IEnumerable<int> c, bool v, Action a",
            "c",
            "a()");
    }
}
