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
public class ForEachToFirstLastTest {
    private void AssertNoOptimization(Assumptions assumptions, string pars, string collection, string body) {
        var tree1 = ("void f(" + pars + ") { foreach (var e in " + collection + ") { " + body + " }").ParseFunctionTreeFromStringUsingStandard();
        var statements1 = (ForEachStatementSyntax)tree1.TestGetParsedFunctionStatements().Single();
        var model = tree1.GetTestSemanticModel();
        Assert.IsTrue(ForEachToFirstLast.GetSimplifications(statements1, model, assumptions).Count() == 0);
    }
    private void AssertOptimizes(Assumptions assumptions, string pars, string collection, string body, string result) {
        var tree1 = ("void f(" + pars + ") { foreach (var e in " + collection + ") { " + body + " }").ParseFunctionTreeFromStringUsingStandard();
        var tree2 = ("void f(" + pars + ") { " + result + "}").ParseFunctionTreeFromStringUsingStandard();
        var statement1 = (ForEachStatementSyntax)tree1.TestGetParsedFunctionStatements().Single();
        var body2 = tree2.TestGetParsedFunctionBody();
        var model = tree1.GetTestSemanticModel();
        var simplifications = ForEachToFirstLast.GetSimplifications(statement1, model, assumptions).ToArray();
        TestUtil.AssertSameSyntax(simplifications.Single().NewNode, body2);
    }

    [TestMethod()]
    public void ForEachToIfFirstLast() {
        AssertOptimizes(
            Assumptions.All,
            "IEnumerable<int> c, int v",
            "c",
            "v = e; break;",
            "var _e = c.Cast<Int32?>().FirstOrDefault(); if (_e != null) { v = _e.Value; }");
        AssertOptimizes(
            Assumptions.All,
            "IEnumerable<int> c, int v",
            "c",
            "v = e; continue;",
            "var _e = c.Cast<Int32?>().LastOrDefault(); if (_e != null) { v = _e.Value; }");
        AssertOptimizes(
            Assumptions.All,
            "IEnumerable<int> c, int v",
            "c",
            "v = e;",
            "var _e = c.Cast<Int32?>().LastOrDefault(); if (_e != null) { v = _e.Value; }");
        AssertOptimizes(
            Assumptions.All,
            "IEnumerable<object> c, object v",
            "c",
            "v = e; break;",
            "var _e = c.Select(e => Tuple.Create(e)).FirstOrDefault(); if (_e != null) { v = _e.Item1; }");
        AssertOptimizes(
            Assumptions.All,
            "IEnumerable<object> c, object v",
            "c",
            "v = e;",
            "var _e = c.Select(e => Tuple.Create(e)).LastOrDefault(); if (_e != null) { v = _e.Item1; }");
        AssertOptimizes(
            Assumptions.All,
            "IEnumerable<int> c, int v",
            "c",
            "var x = e; x += 1; v = x; break;",
            "var _e = c.Cast<Int32?>().FirstOrDefault(); if (_e != null) { var x = _e.Value; x += 1; v = x; }");
        AssertOptimizes(
            Assumptions.All,
            "IEnumerable<int> c, int v",
            "c",
            "var x = e; var y = e; var z = e; v = x + y + z; break;",
            "var _e = c.Cast<Int32?>().FirstOrDefault(); if (_e != null) { var e = _e.Value; var x = e; var y = e; var z = e; v = x + y + z; }");
        AssertOptimizes(
            Assumptions.All,
            "IEnumerable<int> c, int a1, int a2, int a3",
            "c",
            "a1 = e; a2 = e; a3 = e;",
            "var _e = c.Cast<Int32?>().LastOrDefault(); if (_e != null) { var e = _e.Value; a1 = e; a2 = e; a3 = e; }");

        // covered by 'any'
        AssertNoOptimization(
            Assumptions.All,
            "IEnumerable<int> c, bool v, bool m",
            "c",
            "v = m;");

        // not applicable
        AssertNoOptimization(
            Assumptions.None,
            "IEnumerable<int> c, bool v",
            "c",
            "v = e; break;");
    }
}
