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
    [TestMethod()]
    public void OppositeSimplificationsTest() {
        var tree = @"
            bool f(bool b1, bool b2) { 
                return b1 ? b2 : !b2;
            }".ParseFunctionTreeFromString();
        var conditional = tree.Root.DescendentNodes().OfType<ConditionalExpressionSyntax>().Single();
        var replacement = ReducibleConditionalExpression.GetSimplifications(conditional, tree.GetTestSemanticModel()).Single();
        Assert.IsTrue(replacement.OldNode == conditional);
        replacement.NewNode.AssertSameSyntax("b1 == b2".ParseExpressionFromString());
    }
    [TestMethod()]
    public void EquivalentSimplificationsTest() {
        var tree = @"
            bool f(bool b1, bool b2) { 
                return b1 ? b2 : b2;
            }".ParseFunctionTreeFromString();
        var conditional = tree.Root.DescendentNodes().OfType<ConditionalExpressionSyntax>().Single();
        var replacement = ReducibleConditionalExpression.GetSimplifications(conditional, tree.GetTestSemanticModel()).Single();
        Assert.IsTrue(replacement.OldNode == conditional);
        replacement.NewNode.AssertSameSyntax("b2".ParseExpressionFromString());
    }
    [TestMethod()]
    public void UnsafeEquivalentSimplificationsTest() {
        var tree = @"
            bool f(Func<bool> b1, bool b2) { 
                return b1() ? b2 : b2;
            }".ParseFunctionTreeFromString();
        var conditional = tree.Root.DescendentNodes().OfType<ConditionalExpressionSyntax>().Single();
        var replacements = ReducibleConditionalExpression.GetSimplifications(conditional, tree.GetTestSemanticModel()).ToArray();
        Assert.IsTrue(replacements.Length == 2);
        Assert.IsTrue(replacements[0].OldNode == conditional);
        Assert.IsTrue(replacements[1].OldNode == conditional);
        replacements[0].NewNode.AssertSameSyntax("b2".ParseExpressionFromString());
        replacements[1].NewNode.AssertSameSyntax("(b1() && false) || b2".ParseExpressionFromString());
    }
}
