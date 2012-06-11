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
    public static void ReplaceExpressionTest<T>(Func<T, ISemanticModel, IEnumerable<ReplaceAction>> trans, string pars, string original, params string[] results) where T : ExpressionSyntax {
        var code = "bool f(" + pars + ") { return " + original + "; }";
        var tree = code.ParseFunctionTreeFromStringUsingStandard();
        var m = tree.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>().Single();
        var ori = (T)((ReturnStatementSyntax)m.Body.Statements.Single()).Expression;
        var replacements = trans(ori, tree.GetTestSemanticModel()).ToArray();
        Assert.IsTrue(replacements.Length == results.Length);
        Assert.IsTrue(replacements.All(e => e.OldNode == ori));
        foreach (var pair in replacements.Zip(results, (e1, e2) => Tuple.Create(e1, e2)))
            pair.Item1.NewNode.AssertSameSyntax(pair.Item2.ParseExpressionFromString());
    }
    public static void AssertSameSyntax(this SyntaxToken n1, SyntaxToken n2) {
        Assert.IsTrue(n1.Kind == n2.Kind);
    }
    public static void AssertSameSyntax(this SyntaxNode n1, SyntaxNode n2) {
        if (n1 == null && n2 == null) return;
        Assert.IsTrue(n1.Kind == n2.Kind);
        if (n1 is SimpleNameSyntax || n1 is IdentifierNameSyntax || n1 is LiteralExpressionSyntax) {
            Assert.AreEqual(n1.WithoutTrivia().ToString(), n2.WithoutTrivia().ToString());
        } else if (n1 is MemberAccessExpressionSyntax) {
            var m1 = (MemberAccessExpressionSyntax)n1;
            var m2 = (MemberAccessExpressionSyntax)n2;
            AssertSameSyntax(m1.Expression, m2.Expression);
            AssertSameSyntax(m1.Name, m2.Name);
        } else if (n1 is InvocationExpressionSyntax) {
            var m1 = (InvocationExpressionSyntax)n1;
            var m2 = (InvocationExpressionSyntax)n2;
            AssertSameSyntax(m1.Expression, m2.Expression);
            AssertSameSyntax(m1.ArgumentList, m2.ArgumentList);
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
        } else if (n1 is ForEachStatementSyntax) {
            var m1 = (ForEachStatementSyntax)n1;
            var m2 = (ForEachStatementSyntax)n2;
            AssertSameSyntax(m1.Identifier, m2.Identifier);
            AssertSameSyntax(m1.Statement, m2.Statement);
            AssertSameSyntax(m1.Expression, m2.Expression);
        } else if (n1 is BlockSyntax) {
            var m1 = (BlockSyntax)n1;
            var m2 = (BlockSyntax)n2;
            Assert.IsTrue(m1.Statements.Count == m2.Statements.Count);
            for (var i = 0; i < m1.Statements.Count; i++)
                AssertSameSyntax(m1.Statements[i], m2.Statements[i]);
        } else if (n1 is IfStatementSyntax) {
            var m1 = (IfStatementSyntax)n1;
            var m2 = (IfStatementSyntax)n2;
            AssertSameSyntax(m1.Condition, m2.Condition);
            AssertSameSyntax(m1.Statement, m2.Statement);
            AssertSameSyntax(m1.Else, m2.Else);
        } else if (n1 is ElseClauseSyntax) {
            var m1 = (ElseClauseSyntax)n1;
            var m2 = (ElseClauseSyntax)n2;
            AssertSameSyntax(m1.Statement, m2.Statement);
        } else if (n1 is ArgumentListSyntax) {
            var m1 = (ArgumentListSyntax)n1;
            var m2 = (ArgumentListSyntax)n2;
            Assert.IsTrue(m1.Arguments.Count == m2.Arguments.Count);
            for (var i = 0; i < m1.Arguments.Count; i++)
                AssertSameSyntax(m1.Arguments[i], m2.Arguments[i]);
        } else if (n1 is ArgumentSyntax) {
            var m1 = (ArgumentSyntax)n1;
            var m2 = (ArgumentSyntax)n2;
            AssertSameSyntax(m1.NameColon, m2.NameColon);
            if (m1.RefOrOutKeyword != null) Assert.IsTrue(m1.RefOrOutKeyword.Kind == m2.RefOrOutKeyword.Kind);
            AssertSameSyntax(m1.Expression, m2.Expression);
        } else if (n1 is NameColonSyntax) {
            var m1 = (NameColonSyntax)n1;
            var m2 = (NameColonSyntax)n2;
            AssertSameSyntax(m1.Identifier, m2.Identifier);
        } else if (n1 is ExpressionStatementSyntax) {
            var m1 = (ExpressionStatementSyntax)n1;
            var m2 = (ExpressionStatementSyntax)n2;
            AssertSameSyntax(m1.Expression, m2.Expression);
        } else if (n1 is SimpleLambdaExpressionSyntax) {
            var m1 = (SimpleLambdaExpressionSyntax)n1;
            var m2 = (SimpleLambdaExpressionSyntax)n2;
            AssertSameSyntax(m1.Parameter, m2.Parameter);
            AssertSameSyntax(m1.Body, m2.Body);
        } else if (n1 is ParameterListSyntax) {
            var m1 = (ParameterListSyntax)n1;
            var m2 = (ParameterListSyntax)n2;
            Assert.IsTrue(m1.Parameters.Count == m2.Parameters.Count);
            for (var i = 0; i < m1.Parameters.Count; i++)
                AssertSameSyntax(m1.Parameters[i], m2.Parameters[i]);
        } else if (n1 is ParameterSyntax) {
            var m1 = (ParameterSyntax)n1;
            var m2 = (ParameterSyntax)n2;
            AssertSameSyntax(m1.Type, m2.Type);
            AssertSameSyntax(m1.Identifier, m2.Identifier);
        } else if (n1 is LocalDeclarationStatementSyntax) {
            var m1 = (LocalDeclarationStatementSyntax)n1;
            var m2 = (LocalDeclarationStatementSyntax)n2;
            AssertSameSyntax(m1.Declaration, m2.Declaration);
            Assert.IsTrue(m1.Modifiers.Select(x => x.ValueText).SequenceEqual(m2.Modifiers.Select(x => x.ValueText)));
        } else if (n1 is VariableDeclarationSyntax) {
            var m1 = (VariableDeclarationSyntax)n1;
            var m2 = (VariableDeclarationSyntax)n2;
            AssertSameSyntax(m1.Type, m2.Type);
            Assert.IsTrue(m1.Variables.Count == m2.Variables.Count);
            for (var i = 0; i < m1.Variables.Count; i++) {
                AssertSameSyntax(m1.Variables[i], m2.Variables[i]);
            }
        } else if (n1 is VariableDeclaratorSyntax) {
            var m1 = (VariableDeclaratorSyntax)n1;
            var m2 = (VariableDeclaratorSyntax)n2;
            AssertSameSyntax(m1.Identifier, m2.Identifier);
            AssertSameSyntax(m1.Initializer, m2.Initializer);
        } else if (n1 is EqualsValueClauseSyntax) {
            var m1 = (EqualsValueClauseSyntax)n1;
            var m2 = (EqualsValueClauseSyntax)n2;
            AssertSameSyntax(m1.Value, m2.Value);
        } else if (n1 is PredefinedTypeSyntax) {
            var m1 = (PredefinedTypeSyntax)n1;
            var m2 = (PredefinedTypeSyntax)n2;
            Assert.IsTrue(m1.PlainName == m2.PlainName);
        } else if (n1 is EmptyStatementSyntax) {
            Assert.IsTrue(n2 is EmptyStatementSyntax);
        } else if (n1 is ObjectCreationExpressionSyntax) {
            var m1 = (ObjectCreationExpressionSyntax)n1;
            var m2 = (ObjectCreationExpressionSyntax)n2;
            AssertSameSyntax(m1.ArgumentList, m2.ArgumentList);
            AssertSameSyntax(m1.Initializer, m2.Initializer);
            AssertSameSyntax(m1.Type, m2.Type);
        } else {
            throw new NotImplementedException();
        }
    }
    public static StatementSyntax ParseStatementFromString(this String s) {
        var p = SyntaxTree.ParseCompilationUnit(s, options: ParseOptions.Default.WithKind(SourceCodeKind.Interactive));
        var compUnit = (CompilationUnitSyntax)p.GetRoot();
        var globalStatement = (GlobalStatementSyntax)compUnit.ChildNodes().Single();
        return (StatementSyntax)globalStatement.ChildNodes().Single();
    }
    public static ExpressionSyntax ParseExpressionFromString(this String s) {
        return (s.ParseStatementFromString() as ExpressionStatementSyntax).Expression;
    }
    public static SyntaxTree ParseFunctionTreeFromString(this String s) {
        return SyntaxTree.ParseCompilationUnit(s, options: ParseOptions.Default.WithKind(SourceCodeKind.Interactive));
    }
    public static SyntaxTree ParseFunctionTreeFromStringUsingStandard(this String s) {
        var c = "using System; using System.Linq; using System.Collections.Generic; class TEMP__WRAP__CLASS__ {" + s + "}";
        return SyntaxTree.ParseCompilationUnit(c, options: ParseOptions.Default.WithKind(SourceCodeKind.Interactive));
    }
    public static BlockSyntax TestGetParsedFunctionBody(this SyntaxTree tree) {
        var compUnit = (CompilationUnitSyntax)tree.GetRoot();
        var method = compUnit.DescendantNodes().OfType<MethodDeclarationSyntax>().Single();
        return method.Body;
    }
    public static StatementSyntax[] TestGetParsedFunctionStatements(this SyntaxTree tree) {
        return tree.TestGetParsedFunctionBody().Statements.ToArray();
    }
    public static ISemanticModel GetTestSemanticModel(this SyntaxTree tree) {
        return Compilation.Create("temp")
                .AddSyntaxTrees(tree)
                .AddReferences(new AssemblyFileReference(typeof(object).Assembly.Location))
                .GetSemanticModel(tree);
    }
}
