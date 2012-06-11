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
public class TryGetImplicitBranchSingleStatementsTest {
    private void AssertFinds(string pars, string ifStatement, string trueCase, string falseCase) {
        var tree = ("void f("+pars+") {"+ifStatement+"}").ParseFunctionTreeFromString();
        var statement = tree.TestGetParsedFunctionBody().DescendantNodes().OfType<IfStatementSyntax>().Single();
        var model = tree.GetTestSemanticModel();
        var r = statement.TryGetImplicitBranchSingleStatements(model, Assumptions.All);
        Assert.IsTrue(r != null);
        r.True.AssertSameSyntax(trueCase.ParseStatementFromString());
        r.False.AssertSameSyntax(falseCase.ParseStatementFromString());
    }
    private void AssertFails(string pars, string ifStatement) {
        var tree = ("void f(" + pars + ") {" + ifStatement + "}").ParseFunctionTreeFromString();
        var statement = (IfStatementSyntax)tree.TestGetParsedFunctionStatements().Single();
        var model = tree.GetTestSemanticModel();
        var r = statement.TryGetImplicitBranchSingleStatements(model, Assumptions.All);
        Assert.IsTrue(r == null);
    }
    [TestMethod()]
    public void TestTryGetImplicitBranchSingleStatements() {
        AssertFinds(
            "bool b, Action a1, Action a2",
            "if (b) { a1(); } else { a2(); }",
            "a1();",
            "a2();");
        
        AssertFails(
            "bool b, Action a1, Action a2",
            "if (b) { a1(); a1(); } else { a2(); }");
        AssertFails(
            "bool b, Action a1, Action a2",
            "if (b) { a1(); } else { a2(); a2(); }");
    }
}
