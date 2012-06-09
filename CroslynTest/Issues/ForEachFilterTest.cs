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
public class ForEachFilterTest {
    private void SimpleTest(string pars, string collection, string body, string newCollection, string newBody) {
        var tree1 = ("void f(" + pars + ") { foreach (var e in " + collection + ") { " + body + " }").ParseFunctionTreeFromString();
        var tree2 = ("void f(" + pars + ") { foreach (var e in " + newCollection + ") { " + newBody + " }").ParseFunctionTreeFromString();
        var statements1 = (ForEachStatementSyntax)tree1.TestGetParsedFunctionStatements().Single();
        var statements2 = (ForEachStatementSyntax)tree2.TestGetParsedFunctionStatements().Single();
        var model = tree1.GetTestSemanticModel();
        TestUtil.AssertSameSyntax(ForEachFilter.GetSimplifications(statements1, model).Single().NewNode, statements2);
    }

    [TestMethod()]
    public void FilterTest() {
        SimpleTest("IEnumerable<int> c, Func<int, bool> b, Action a",
                   "c",
                   "if (b(e)) { a(); }",
                   "c.Where(e => b(e))",
                   "a()");
    }
}
