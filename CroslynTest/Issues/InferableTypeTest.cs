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
public class InferableTypeTest {
    private void AssertDoesNotOptimizeD(string pars, string declaration) {
        var tree1 = ("void f(" + pars + ") { " + declaration + "; }").ParseFunctionTreeFromStringUsingStandard();
        var declarationStatement = (LocalDeclarationStatementSyntax)tree1.TestGetParsedFunctionStatements().Single();
        var model = tree1.GetTestSemanticModel();
        Assert.IsTrue(InferableType.GetSimplifications(declarationStatement, model, Assumptions.All).None());
    }
    private void AssertDoesNotOptimizeL(string pars, string declaration) {
        var tree1 = ("void f(" + pars + ") { " + declaration + " }").ParseFunctionTreeFromStringUsingStandard();
        var loop = (ForEachStatementSyntax)tree1.TestGetParsedFunctionStatements().Single();
        var model = tree1.GetTestSemanticModel();
        Assert.IsTrue(InferableType.GetSimplifications(loop, model, Assumptions.All).None());
    }
    private void AssertOptimizesD(string pars, string declaration, string newBody) {
        var tree1 = ("void f(" + pars + ") { " + declaration + "; }").ParseFunctionTreeFromStringUsingStandard();
        var tree2 = ("void f(" + pars + ") { " + newBody + "; }").ParseFunctionTreeFromStringUsingStandard();
        var declarationStatement = (LocalDeclarationStatementSyntax)tree1.TestGetParsedFunctionStatements().Single();
        var declarationStatement2 = (LocalDeclarationStatementSyntax)tree2.TestGetParsedFunctionStatements().Single();
        var model = tree1.GetTestSemanticModel();
        var s = InferableType.GetSimplifications(declarationStatement, model, Assumptions.All).Single();
        var nb = declarationStatement.ReplaceNode(s.OldNode, s.NewNode);
        nb.AssertSameSyntax(declarationStatement2);
    }
    private void AssertOptimizesL(string pars, string declaration, string newBody) {
        var tree1 = ("void f(" + pars + ") { " + declaration + " }").ParseFunctionTreeFromStringUsingStandard();
        var tree2 = ("void f(" + pars + ") { " + newBody + " }").ParseFunctionTreeFromStringUsingStandard();
        var loop = (ForEachStatementSyntax)tree1.TestGetParsedFunctionStatements().Single();
        var loop2 = (ForEachStatementSyntax)tree2.TestGetParsedFunctionStatements().Single();
        var model = tree1.GetTestSemanticModel();
        var s = InferableType.GetSimplifications(loop, model, Assumptions.All).Single();
        var nb = loop.ReplaceNode(s.OldNode, s.NewNode);
        nb.AssertSameSyntax(loop2);
    }

    [TestMethod()]
    public void TestInferableType() {
        AssertOptimizesD(
            "",
            "string s = \"\"",
            "var s = \"\"");
        AssertOptimizesD(
            "object r",
            "object s = r",
            "var s = r");
        AssertOptimizesD(
            "dynamic r",
            "dynamic s = r",
            "var s = r");
        AssertOptimizesD(
            "Func<string> f",
            "string s = f()",
            "var s = f()");
        AssertOptimizesD(
            "",
            "uint c = 0u",
            "var c = 0u");
        AssertOptimizesD(
            "",
            "int c = 0",
            "var c = 0");
        AssertOptimizesD(
            "",
            "int[] x = new int[] {0, 1}",
            "var x = new int[] {0, 1}");
        AssertOptimizesD(
            "",
            "int[] x = new[] {0, 1}",
            "var x = new[] {0, 1}");

        AssertDoesNotOptimizeD(
            "",
            "int c = 0, d = 0");
        AssertDoesNotOptimizeD(
            "",
            "object s = \"\"");
        AssertDoesNotOptimizeD(
            "",
            "long c");
        AssertDoesNotOptimizeD(
            "",
            "long c = 0");
        AssertDoesNotOptimizeD(
            "",
            "string s = null");
        AssertDoesNotOptimizeD(
            "",
            "byte c = 0");
        AssertDoesNotOptimizeD(
            "",
            "uint c = 0");

        AssertOptimizesL(
            "",
            "foreach (string s in new[] {\"\"});",
            "foreach (var s in new[] {\"\"});");
        AssertOptimizesL(
            "",
            "foreach (int s in new[] {0, 1});",
            "foreach (var s in new[] {0, 1});");
        AssertOptimizesL(
            "",
            "foreach (long s in new long[] {0, 1});",
            "foreach (var s in new long[] {0, 1});");
        AssertOptimizesL(
            "IEnumerable<dynamic> d",
            "foreach (dynamic s in d);",
            "foreach (var s in d);");
        AssertDoesNotOptimizeL(
            "",
            "foreach (object s in new[] {\"\"});");
    }
}
