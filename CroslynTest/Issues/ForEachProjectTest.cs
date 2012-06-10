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
public class ForEachProjectTest {
    private void AssertNoOptimization(string pars, string collection, string body) {
        var tree1 = ("void f(" + pars + ") { foreach (var e in " + collection + ") { " + body + " }").ParseFunctionTreeFromString();
        var statements1 = (ForEachStatementSyntax)tree1.TestGetParsedFunctionStatements().Single();
        var model = tree1.GetTestSemanticModel();
        Assert.IsTrue(ForEachProject.GetSimplifications(statements1, model).Count() == 0);
    }
    private void AssertOptimizes(string pars, string collection, string body, string newCollection, string newBody) {
        var tree1 = ("void f(" + pars + ") { foreach (var e in " + collection + ") { " + body + " }").ParseFunctionTreeFromString();
        var tree2 = ("void f(" + pars + ") { foreach (var e in " + newCollection + ") { " + newBody + " }").ParseFunctionTreeFromString();
        var statements1 = (ForEachStatementSyntax)tree1.TestGetParsedFunctionStatements().Single();
        var statements2 = (ForEachStatementSyntax)tree2.TestGetParsedFunctionStatements().Single();
        var model = tree1.GetTestSemanticModel();
        TestUtil.AssertSameSyntax(ForEachProject.GetSimplifications(statements1, model).Single().NewNode, statements2);
    }

    [TestMethod()]
    public void FilterTest() {
        AssertOptimizes(
            "IEnumerable<int> c, Func<int, bool> b, Action<int> a",
            "c",
            "if (b(e + 1)) { a(); }",
            "c.Select(e => e + 1)",
            "if (b(e)) { a(); }");
        AssertOptimizes(
            "IEnumerable<int> c, Action<int> a",
            "c",
            "a(e * 2);",
            "c.Select(e => e * 2)",
            "a(e)");

        AssertNoOptimization(
            "IEnumerable<int> c, Func<int, bool> b, Action<int> a",
            "c",
            "if (b(e + 1)) a(e);");
        AssertNoOptimization(
            "IEnumerable<int> c, Action<int> a",
            "c",
            "a(e + 1); a(e + 2);");
        AssertNoOptimization(
            "IEnumerable<int> c, Action<int> a",
            "c",
            "a(e)");
    }
}
