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
public class IsIdempotentTest {
    private bool? Idempotence(string pars, string statement, Assumptions? assume = null) {
        var tree = ("void f(" + pars + ") {" + statement + "}").ParseFunctionTreeFromString();
        var statementN = tree.TestGetParsedFunctionBody();
        return statementN.IsIdempotent(tree.GetTestSemanticModel(), assume ?? Assumptions.All);
    }
    [TestMethod()]
    public void TestIsIdempotent() {
        // assignment
        Assert.IsTrue(Idempotence("int x", "x = 1;") == true);

        // non-idempotent adjustments
        Assert.IsTrue(Idempotence("int x", "x += 1;") != true);
        Assert.IsTrue(Idempotence("int x", "x -= 1;") != true);
        Assert.IsTrue(Idempotence("int x", "x *= 2;") != true);
        Assert.IsTrue(Idempotence("int x", "x /= 2;") != true);
        Assert.IsTrue(Idempotence("int x", "x ^= 1;") != true);
        Assert.IsTrue(Idempotence("int x", "x >>= 1;") != true);
        Assert.IsTrue(Idempotence("int x", "x <<= 1;") != true);
        Assert.IsTrue(Idempotence("int x", "x++;") != true);
        Assert.IsTrue(Idempotence("int x", "x--;") != true);
        Assert.IsTrue(Idempotence("int x", "++x;") != true);
        Assert.IsTrue(Idempotence("int x", "--x;") != true);

        // idempotent adjustments
        Assert.IsTrue(Idempotence("int x", "x >>= 0;") != false);
        Assert.IsTrue(Idempotence("int x", "x <<= 0;") != false);
        Assert.IsTrue(Idempotence("int x", "x *= 0;") != false);
        Assert.IsTrue(Idempotence("int x", "x *= 1;") != false);
        Assert.IsTrue(Idempotence("int x", "x /= 1;") != false);
        Assert.IsTrue(Idempotence("int x", "x |= 1;") != false);
        Assert.IsTrue(Idempotence("int x", "x &= 1;") != false);
        Assert.IsTrue(Idempotence("int x", "x %= 1;") != false);
        Assert.IsTrue(Idempotence("int x", "x %= 23;") != false);

        //drain
        Assert.IsTrue(Idempotence("int x1, int x2", "x1 = x2; x2 = 0;") != true);

        //multi-assign
        Assert.IsTrue(Idempotence("int x1, int x2", "x1 = 0; x2 = 0;") == true);
    }
}
