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
public class ReducibleConditionalExpressionTest {
    private void SimpleTest(string pars, string original, params string[] results) {
        TestUtil.ReplaceExpressionTest<ConditionalExpressionSyntax>(
            (e,m)=>ReducibleConditionalExpression.GetSimplifications(e,m), 
            pars, 
            original, 
            results);
    }

    [TestMethod()]
    public void Inverses() {
        SimpleTest("bool b1, bool b2",
                   "b1 ? b2 : !b2",
                   "b1 == b2");
        SimpleTest("bool b1",
                   "b1 ? true : false",
                   "b1 == true");
    }
    [TestMethod()]
    public void EquivalentSimplificationsTest() {
        SimpleTest("bool b1, bool b2", 
                   "b1 ? b2 : b2", 
                   "b2");
        SimpleTest("bool b1",
                   "b1 ? true : true",
                   "true");
    }
    [TestMethod()]
    public void UnsafeEquivalentSimplificationsTest() {
        SimpleTest("Func<bool> b1, bool b2",
                   "b1() ? b2 : b2",
                   "b2",
                   "(b1() && false) || b2");
        SimpleTest("Func<bool> b1",
                   "b1() ? true : true",
                   "true",
                   "(b1() && false) || true");
    }
}
