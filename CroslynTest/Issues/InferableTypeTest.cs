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
    private void AssertDoesNotOptimize<T>(string pars, string declaration, Func<T, ISemanticModel, Assumptions, IEnumerable<ReplaceAction>> f) where T : SyntaxNode {
        var tree = ("void f(" + pars + ") { " + declaration + "; }").ParseFunctionTreeFromStringUsingStandard();
        var body = tree.TestGetParsedFunctionBody();
        var target = body.DescendantNodes().OfType<T>().Single();
        var model = tree.GetTestSemanticModel();
        Assert.IsTrue(f(target, model, Assumptions.All).None());
    }
    private void AssertOptimizes<T>(string pars, string bodyText, string newBodyText, Func<T, ISemanticModel, Assumptions, IEnumerable<ReplaceAction>> f) where T : SyntaxNode {
        var tree1 = ("void f(" + pars + ") { " + bodyText + "; }").ParseFunctionTreeFromStringUsingStandard();
        var tree2 = ("void f(" + pars + ") { " + newBodyText + "; }").ParseFunctionTreeFromStringUsingStandard();
        var body = tree1.TestGetParsedFunctionBody();
        var body2 = tree2.TestGetParsedFunctionBody();
        var target = body.DescendantNodes().OfType<T>().Single();
        var model = tree1.GetTestSemanticModel();
        var s = f(target, model, Assumptions.All).Single();
        var newBody = body.ReplaceNode(s.OldNode, s.NewNode);
        body2.AssertSameSyntax(newBody);
    }

    private void AssertDoesNotOptimizeD(string pars, string declaration) {
        AssertDoesNotOptimize<VariableDeclarationSyntax>(pars, declaration, (a, b, c) => InferableType.GetSimplifications(a, b, c));
    }
    private void AssertDoesNotOptimizeL(string pars, string declaration) {
        AssertDoesNotOptimize<ForEachStatementSyntax>(pars, declaration, (a, b, c) => InferableType.GetSimplifications(a, b, c));
    }
    private void AssertOptimizesD(string pars, string declaration, string newBody) {
        AssertOptimizes<VariableDeclarationSyntax>(pars, declaration, newBody, (a, b, c) => InferableType.GetSimplifications(a, b, c));
    }
    private void AssertOptimizesL(string pars, string declaration, string newBody) {
        AssertOptimizes<ForEachStatementSyntax>(pars, declaration, newBody, (a, b, c) => InferableType.GetSimplifications(a, b, c));
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
        AssertOptimizesD(
            "object r",
            "for (object s = r; s != null; s = null);",
            "for (var s = r; s != null; s = null);");
        AssertOptimizesD(
            "object r",
            "for (int[] x = new int[] {0, 1}; x != null; x = null);",
            "for (var x = new int[] {0, 1}; x != null; x = null);");

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
        AssertDoesNotOptimizeD(
            "",
            "for (uint c = 0;;);");
        AssertDoesNotOptimizeD(
            "",
            "for (int c = 0, c2 = 0;;);");

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
