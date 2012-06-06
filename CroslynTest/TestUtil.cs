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
public static class TestUtil {
    public static void AssertSameSyntax(this SyntaxToken n1, SyntaxToken n2) {
        Assert.IsTrue(n1.Kind == n2.Kind);
    }
    public static void AssertSameSyntax(this SyntaxNode n1, SyntaxNode n2) {
        Assert.IsTrue(n1.Kind == n2.Kind);
        if (n1 is SimpleNameSyntax || n1 is IdentifierNameSyntax || n1 is LiteralExpressionSyntax) {
            Assert.AreEqual(n1.WithoutTrivia().ToString(), n2.WithoutTrivia().ToString());
        } else if (n1 is MemberAccessExpressionSyntax) {
            var m1 = (MemberAccessExpressionSyntax)n1;
            var m2 = (MemberAccessExpressionSyntax)n2;
            AssertSameSyntax(m1.Expression, m2.Expression);
            Assert.IsTrue(m1.Name == m2.Name);
        } else if (n1 is InvocationExpressionSyntax) {
            var m1 = (InvocationExpressionSyntax)n1;
            var m2 = (InvocationExpressionSyntax)n2;
            AssertSameSyntax(m1.Expression, m2.Expression);
            Assert.IsTrue(m1.ArgumentList.Arguments.Count == m2.ArgumentList.Arguments.Count);
            for (var i = 0; i < m1.ArgumentList.Arguments.Count; i++) {
                AssertSameSyntax(m1.ArgumentList.Arguments[i], m2.ArgumentList.Arguments[i]);
            }
        } else if (n1 is BinaryExpressionSyntax) {
            var m1 = (BinaryExpressionSyntax)n1;
            var m2 = (BinaryExpressionSyntax)n2;
            AssertSameSyntax(m1.Left, m2.Left);
            AssertSameSyntax(m1.Right, m2.Right);
            AssertSameSyntax(m1.OperatorToken, m2.OperatorToken);
        } else if (n1 is ConditionalExpressionSyntax) {
            var m1 = (ConditionalExpressionSyntax)n1;
            var m2 = (ConditionalExpressionSyntax)n2;
            AssertSameSyntax(m1.Condition, m2.Condition);
            AssertSameSyntax(m1.WhenTrue, m2.WhenTrue);
            AssertSameSyntax(m1.WhenFalse, m2.WhenFalse);
        } else if (n1 is PrefixUnaryExpressionSyntax) {
            var m1 = (PrefixUnaryExpressionSyntax)n1;
            var m2 = (PrefixUnaryExpressionSyntax)n2;
            AssertSameSyntax(m1.Operand, m2.Operand);
            AssertSameSyntax(m1.OperatorToken, m2.OperatorToken);
        } else if (n1 is ParenthesizedExpressionSyntax) {
            var m1 = (ParenthesizedExpressionSyntax)n1;
            var m2 = (ParenthesizedExpressionSyntax)n2;
            AssertSameSyntax(m1.Expression, m2.Expression);
        } else {
            throw new NotImplementedException();
        }
    }
    public static ExpressionSyntax ParseExpressionFromString(this String s) {
        var p = SyntaxTree.ParseCompilationUnit(s, options: ParseOptions.Default.WithKind(SourceCodeKind.Interactive));
        var compUnit = (CompilationUnitSyntax)p.GetRoot();
        var globalStatement = (GlobalStatementSyntax)compUnit.ChildNodes().Single();
        var expStatement = (ExpressionStatementSyntax)globalStatement.ChildNodes().Single();
        return expStatement.Expression;
    }
    public static SyntaxTree ParseFunctionTreeFromString(this String s) {
        return SyntaxTree.ParseCompilationUnit(s, options: ParseOptions.Default.WithKind(SourceCodeKind.Interactive));
    }
    public static StatementSyntax[] TestGetParsedFunctionStatements(this SyntaxTree tree) {
        var compUnit = (CompilationUnitSyntax)tree.GetRoot();
        var method = (MethodDeclarationSyntax)compUnit.ChildNodes().Single();
        return method.Body.Statements.ToArray();
    }
    public static ISemanticModel GetTestSemanticModel(this SyntaxTree tree) {
        return Compilation.Create("temp")
                .AddSyntaxTrees(tree)
                .AddReferences(new AssemblyFileReference(typeof(object).Assembly.Location))
                .GetSemanticModel(tree);
    }
}
