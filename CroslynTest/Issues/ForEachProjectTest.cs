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
        var tree1 = ("void f(" + pars + ") { foreach (var e in " + collection + ") { " + body + " }").ParseFunctionTreeFromStringUsingStandard();
        var statements1 = (ForEachStatementSyntax)tree1.TestGetParsedFunctionStatements().Single();
        var model = tree1.GetTestSemanticModel();
        Assert.IsTrue(ForEachProject.GetSimplifications(statements1, model).Count() == 0);
    }
    private void AssertOptimizes(string pars, string collection, string body, string newCollection, string newBody) {
        var tree1 = ("void f(" + pars + ") { foreach (var e in " + collection + ") { " + body + " }").ParseFunctionTreeFromStringUsingStandard();
        var tree2 = ("void f(" + pars + ") { foreach (var e in " + newCollection + ") { " + newBody + " }").ParseFunctionTreeFromStringUsingStandard();
        var statements1 = (ForEachStatementSyntax)tree1.TestGetParsedFunctionStatements().Single();
        var statements2 = (ForEachStatementSyntax)tree2.TestGetParsedFunctionStatements().Single();
        var model = tree1.GetTestSemanticModel();
        TestUtil.AssertSameSyntax(ForEachProject.GetSimplifications(statements1, model).Single().NewNode, statements2);
    }

    [TestMethod()]
    public void ProjectTest() {
        AssertOptimizes(
            "IEnumerable<int> c, Func<int, bool> b, Action<int> a",
            "c",
            "if (b(e + 1)) { a(); }",
            "c.Select(e => b(e + 1))",
            "if (e) { a(); }");
        AssertOptimizes(
            "IEnumerable<int> c, Action<int> a",
            "c",
            "a(e * 2);",
            "c.Select(e => e * 2)",
            "a(e)");
        AssertOptimizes(
            "IEnumerable<int> c, Func<int, int> p, Action<int> a",
            "c",
            "a(p(e));",
            "c.Select(e => p(e))",
            "a(e)");
        AssertOptimizes(
            "IEnumerable<int> c, Func<int, bool> b, Action<int> a",
            "c",
            "var z = e == 0 ? 1 : 0",
            "c.Select(e => e == 0 ? 1 : 0)",
            "var z = e");

        AssertNoOptimization(
            "IEnumerable<int> c, Func<int, bool> b, Action<int> a",
            "c",
            "var z = b(0) ? e : 0");
        AssertNoOptimization(
            "IEnumerable<int> c, Func<int, bool> b, Action<int> a",
            "c",
            "if (b(e + 1)) a(e);");
        AssertNoOptimization(
            "IEnumerable<int> c, Action<int> a",
            "c",
            "a(e + 1); a(e + 2);");
        AssertNoOptimization(
            "IEnumerable<int> c, Action<int> a, Action<int, int> p",
            "c",
            "for (var i = 0; i < 2; i++) a(p(e));");
        AssertNoOptimization(
            "IEnumerable<int> c, Action<int> a, Action<int, int> p",
            "c",
            "while (true) a(p(e));");
        AssertNoOptimization(
            "IEnumerable<int> c, Action<int> a, Action<int, int> p",
            "c",
            "try { a(p(e)); } catch (Exception) {}");
        AssertNoOptimization(
            "IEnumerable<int> c, Action<int> a, Action<int, int> p",
            "c",
            "Action q = () => p(e); q(); q();");
        AssertNoOptimization(
            "IEnumerable<int> c, Action<int> a",
            "c",
            "a(e)");
    }
}
