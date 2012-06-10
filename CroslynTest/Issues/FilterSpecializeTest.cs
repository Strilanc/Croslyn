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
public class FilterSpecializeTest {
    private void SimpleTest(string pars, string original, params string[] results) {
        TestUtil.ReplaceExpressionTest<InvocationExpressionSyntax>(
            (e, m) => FilterSpecialize.GetSimplifications(e, m),
            pars,
            original,
            results);
    }
    [TestMethod()]
    public void ReducibleBooleanExpressionSimpleTests() {
        SimpleTest("IEnumerable<int> x", "x.Where(e => e == 1).Any()", "x.Any(e => e == 1)");
        SimpleTest("IEnumerable<int> x", "x.Where(e => e == 1).First()", "x.First(e => e == 1)");
        SimpleTest("IEnumerable<int> x", "x.Where(e => e == 1).FirstOrDefault()", "x.FirstOrDefault(e => e == 1)");
        SimpleTest("IEnumerable<int> x", "x.Where(e => e == 1).Last()", "x.Last(e => e == 1)");
        SimpleTest("IEnumerable<int> x", "x.Where(e => e == 1).LastOrDefault()", "x.LastOrDefault(e => e == 1)");
        SimpleTest("IList<int> x", "x.Where(e => e == 1).Any()", "x.Any(e => e == 1)");
        SimpleTest("List<int> x", "x.Where(e => e == 1).First()", "x.First(e => e == 1)");

        // no effect
        SimpleTest("IEnumerable<int> x", "x.Where(e => e == 1)");
        SimpleTest("IEnumerable<int> x", "x.Any()");
        SimpleTest("IEnumerable<int> x", "x.First()");
        SimpleTest("IEnumerable<int> x", "x.FirstOrDefault(e => e == 1)");
        SimpleTest("IEnumerable<int> x", "x.GetEnumerator()");
        SimpleTest("IList<int> x", "x.GetEnumerator()");
    }
}
