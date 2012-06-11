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
public class ReducibleBooleanExpressionTest {
    private void SimpleTest(string pars, string original, params string[] results) {
        TestUtil.ReplaceExpressionTest<BinaryExpressionSyntax>(
            (e, m) => ReducibleBooleanExpression.GetSimplifications(e, m, Assumptions.All),
            pars,
            original,
            results);
    }
    [TestMethod()]
    public void ReducibleBooleanExpressionSimpleTests() {
        SimpleTest("bool b", "true == false", "false");
        SimpleTest("bool b", "true == true", "true");
        SimpleTest("bool b", "false != false", "false");
        SimpleTest("bool b", "false && true", "false");
        SimpleTest("bool b", "false || true", "true");
        SimpleTest("bool b", "b || !b", "true");
        SimpleTest("bool b", "b && !b", "false");
        SimpleTest("bool b", "b == true", "b");
        SimpleTest("bool b", "b || true", "true");
        SimpleTest("Func<bool> b", "b() || true");
        SimpleTest("bool b", "b && true", "b");
        SimpleTest("Func<bool> b", "b() && true", "b()");
        SimpleTest("Func<bool> b", "true && b()", "b()");
        SimpleTest("Func<bool> b", "b() && false");
        SimpleTest("bool b", "b == false", "(!b)");
    }
}
