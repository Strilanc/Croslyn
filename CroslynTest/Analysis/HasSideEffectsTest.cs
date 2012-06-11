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
public class HasSideEffectsTest {
    private bool? Effects(string pars, string statement, Assumptions? assume = null) {
        var tree = ("void f(" + pars + ") {" + statement + "}").ParseFunctionTreeFromString();
        var statementN = tree.TestGetParsedFunctionBody();
        return statementN.HasSideEffects(tree.GetTestSemanticModel(), assume ?? Assumptions.All);
    }
    [TestMethod()]
    public void TestHasSideEffects() {
        Assert.IsTrue(Effects("", "1;") == false);
        Assert.IsTrue(Effects("dynamic x", "x;") == false);
        Assert.IsTrue(Effects("", "default(bool);") == false);
        Assert.IsTrue(Effects("", ";") == false);
        Assert.IsTrue(Effects("", "{}") == false);
        Assert.IsTrue(Effects("", "(1);") == false);
        Assert.IsTrue(Effects("dynamic x", "(x++);") == true);
        Assert.IsTrue(Effects("Func<int> x", "x();") == null);
        Assert.IsTrue(Effects("dynamic x", "x.member;", Assumptions.All) == false);
        Assert.IsTrue(Effects("dynamic x", "x.member;", Assumptions.None) == null);
        Assert.IsTrue(Effects("dynamic x", "~!-x + x - x / x * x % x & x && x || x | x ^ x == x != x > x >= x < x <= x ?? x << x >> x", Assumptions.All) == false);
        Assert.IsTrue(Effects("dynamic x", "~!-x + x - x / x * x % x & x && x || x | x ^ x == x != x > x >= x < x <= x ?? x << x >> x", Assumptions.None) == null);

        //assignments
        Assert.IsTrue(Effects("dynamic x", "x--") == true);
        Assert.IsTrue(Effects("dynamic x", "x++") == true);
        Assert.IsTrue(Effects("dynamic x", "++x") == true);
        Assert.IsTrue(Effects("dynamic x", "--x") == true);
        Assert.IsTrue(Effects("dynamic x", "x += 1") == true);
        Assert.IsTrue(Effects("dynamic x", "x -= 1") == true);
        Assert.IsTrue(Effects("dynamic x", "x *= 1") == true);
        Assert.IsTrue(Effects("dynamic x", "x /= 1") == true);
        Assert.IsTrue(Effects("dynamic x", "x *= 1") == true);
        Assert.IsTrue(Effects("dynamic x", "x %= 1") == true);
        Assert.IsTrue(Effects("dynamic x", "x &= 1") == true);
        Assert.IsTrue(Effects("dynamic x", "x |= 1") == true);
        Assert.IsTrue(Effects("dynamic x", "x ^= 1") == true);
        Assert.IsTrue(Effects("dynamic x", "x <<= 1") == true);
        Assert.IsTrue(Effects("dynamic x", "x >>= 1") == true);
        Assert.IsTrue(Effects("dynamic x", "x = 1") == true);

        Assert.IsTrue(Effects("bool b, bool t, bool f", "if (b ? t : f);") == false);
        Assert.IsTrue(Effects("bool b, bool t, Func<bool> f", "if (b ? t : f());") == null);
    }
}
