using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Roslyn.Compilers.CSharp;
using System.Diagnostics.Contracts;
using Roslyn.Services;
using Roslyn.Compilers.Common;
using LinqToCollections.Extra;
using Strilbrary.Collections;
using Strilbrary.Values;
using Roslyn.Services.Editor;
using Roslyn.Compilers;

public static class TrivialTransforms {
    public static BlockSyntax Block(this IEnumerable<StatementSyntax> statements) {
        Contract.Requires(statements != null);
        return Syntax.Block(statements: Syntax.List(statements));
    }
    public static SyntaxList<T> List<T>(this IEnumerable<T> items) where T : SyntaxNode {
        return Syntax.List(items);
    }
    public static SyntaxList<T> List1<T>(this T item) where T : SyntaxNode {
        return Syntax.List(item);
    }
    public static SeparatedSyntaxList<T> SepList1<T>(this T item) where T : SyntaxNode {
        return Syntax.SeparatedList(item);
    }
    public static SeparatedSyntaxList<T> SepBy<T>(this IEnumerable<T> items, SyntaxToken seperator) where T : SyntaxNode {
        return Syntax.SeparatedList(items, seperator.RepeatForever().Take(items.Count() - 1));
    }
    public static ArgumentSyntax Arg(this ExpressionSyntax item) {
        return Syntax.Argument(expression: item);
    }
    public static ArgumentListSyntax Args1(this ExpressionSyntax item) {
        return Syntax.ArgumentList(arguments: item.Arg().SepList1());
    }
    public static ParameterListSyntax Pars(this IEnumerable<ParameterSyntax> pars) {
        return Syntax.ParameterList(parameters: pars.SepBy(Syntax.Token(SyntaxKind.CommaToken)));
    }
    public static TypeArgumentListSyntax TypeArgs1(this TypeSyntax syntax) {
        return Syntax.TypeArgumentList(arguments: Syntax.SeparatedList(syntax));
    }
    public static GenericNameSyntax Genericed(this SyntaxToken identifier, TypeSyntax genericType1) {
        return Syntax.GenericName(identifier, genericType1.TypeArgs1());
    }
    public static NullableTypeSyntax Nullable(this TypeSyntax t) {
        return Syntax.NullableType(t);
    }
    public static MemberAccessExpressionSyntax Accessing(this ExpressionSyntax instance, SimpleNameSyntax member) {
        return Syntax.MemberAccessExpression(SyntaxKind.MemberAccessExpression, instance, name: member);
    }
    public static MemberAccessExpressionSyntax Accessing(this ExpressionSyntax instance, String name) {
        return Syntax.MemberAccessExpression(SyntaxKind.MemberAccessExpression, instance, name: name.AsIdentifier());
    }
    public static InvocationExpressionSyntax Invoking(this ExpressionSyntax target, ArgumentListSyntax args = null) {
        return Syntax.InvocationExpression(target, args ?? Syntax.ArgumentList());
    }
    public static SimpleLambdaExpressionSyntax Lambdad(this SyntaxToken parameter, ExpressionSyntax body) {
        return Syntax.SimpleLambdaExpression(Syntax.Parameter(identifier: parameter), body: body);
    }
    public static SimpleNameSyntax AsIdentifier(this String name) {
        return Syntax.IdentifierName(name);
    }
    public static LocalDeclarationStatementSyntax VarInit(this SimpleNameSyntax name, ExpressionSyntax value) {
        return name.Identifier.VarInit(value);
    }
    public static LocalDeclarationStatementSyntax VarInit(this SyntaxToken name, ExpressionSyntax value) {
        return Syntax.LocalDeclarationStatement(declaration: Syntax.VariableDeclaration(
            Syntax.IdentifierName(Syntax.Token(SyntaxKind.VarKeyword)),
            Syntax.VariableDeclarator(
                name,
                initializerOpt: Syntax.EqualsValueClause(
                    value: value)).SepList1()));
    }
    public static ExpressionStatementSyntax VarAssign(this ExpressionSyntax lhs, ExpressionSyntax value) {
        return Syntax.ExpressionStatement(Syntax.BinaryExpression(SyntaxKind.AssignExpression, lhs, Syntax.Token(SyntaxKind.EqualsToken), value));
    }
    public static IfStatementSyntax IfThen(this ExpressionSyntax condition, StatementSyntax conditionalAction, StatementSyntax alternativeAction = null) {
        return Syntax.IfStatement(
            condition: condition,
            statement: conditionalAction,
            elseOpt: alternativeAction == null ? null : Syntax.ElseClause(statement: alternativeAction));
    }
    public static ConditionalExpressionSyntax Conditional(this ExpressionSyntax condition, ExpressionSyntax whenTrue, ExpressionSyntax whenFalse) {
        return Syntax.ConditionalExpression(
            condition: condition,
            whenTrue: whenTrue,
            whenFalse: whenFalse);
    }
    public static ExpressionSyntax BOpNotEquals(this ExpressionSyntax lhs, ExpressionSyntax rhs) {
        return Syntax.BinaryExpression(SyntaxKind.NotEqualsExpression, lhs, Syntax.Token(SyntaxKind.ExclamationEqualsToken), rhs);
    }
    public static ExpressionSyntax BOpEquals(this ExpressionSyntax lhs, ExpressionSyntax rhs) {
        return Syntax.BinaryExpression(SyntaxKind.EqualsExpression, lhs, Syntax.Token(SyntaxKind.EqualsEqualsToken), rhs);
    }
    public static ExpressionSyntax BOpXor(this ExpressionSyntax lhs, ExpressionSyntax rhs) {
        return Syntax.BinaryExpression(SyntaxKind.ExclusiveOrExpression, lhs, Syntax.Token(SyntaxKind.CaretToken), rhs);
    }
    public static ExpressionSyntax BOpLogicalAnd(this ExpressionSyntax lhs, ExpressionSyntax rhs) {
        return Syntax.BinaryExpression(SyntaxKind.LogicalAndExpression, lhs, Syntax.Token(SyntaxKind.AmpersandAmpersandToken), rhs);
    }
    public static ExpressionSyntax BOpLogicalOr(this ExpressionSyntax lhs, ExpressionSyntax rhs) {
        return Syntax.BinaryExpression(SyntaxKind.LogicalOrExpression, lhs, Syntax.Token(SyntaxKind.BarBarToken), rhs);
    }
    public static ExpressionSyntax BOpAssigned(this ExpressionSyntax lhs, ExpressionSyntax rhs) {
        return Syntax.BinaryExpression(SyntaxKind.AssignExpression, lhs, Syntax.Token(SyntaxKind.EqualsToken), rhs);
    }
    public static CodeIssue[] CodeIssues1(this ICodeAction action, CodeIssue.Severity severity, TextSpan span, String description) {
        Contract.Requires(action != null);
        return new[] { new CodeIssue(severity, span, description, new[] { action }) };
    }
    public enum Placement { Before, After, Around }
    public static T IncludingTriviaSurrounding<T>(this T node, SyntaxNode other, Placement othersRelativePosition) where T : SyntaxNode {
        switch (othersRelativePosition) {
            case Placement.Before:
                return node.WithLeadingTrivia(other.GetLeadingTrivia().Concat(other.GetTrailingTrivia()).Concat(node.GetLeadingTrivia()).DistinctBy(e => e.ToString()));
            case Placement.After:
                return node.WithTrailingTrivia(node.GetTrailingTrivia().Concat(other.GetLeadingTrivia()).Concat(other.GetTrailingTrivia()).DistinctBy(e => e.ToString()));
            case Placement.Around:
                return node.WithLeadingTrivia(other.GetLeadingTrivia().Concat(node.GetLeadingTrivia()).DistinctBy(e => e.ToString()))
                           .WithTrailingTrivia(node.GetTrailingTrivia().Concat(other.GetTrailingTrivia()).DistinctBy(e => e.ToString()));
            default:
                throw new ArgumentException();
        }
    }
    public static StatementSyntax TryWithNewRightHandSideOfAssignmentOrSingleInit(this StatementSyntax syntax, ExpressionSyntax rhs) {
        if (syntax.IsAssignment()) {
            var s = (ExpressionStatementSyntax)syntax;
            var b = (BinaryExpressionSyntax)s.Expression;
            return s.With(expression: b.With(right: rhs));
        }
        if (syntax.IsSingleInitialization()) {
            var d = (LocalDeclarationStatementSyntax)syntax;
            return d.With(declaration: d.Declaration.With(variables: Syntax.SeparatedList(d.Declaration.Variables.Single().With(initializerOpt: Syntax.EqualsValueClause(value: rhs)))));
        }
        return null;
    }
    public static StatementSyntax TryUpdateRHSForAssignmentOrInitOrReturn(this StatementSyntax syntax, ExpressionSyntax rhs) {
        if (syntax.IsReturnValue())
            return ((ReturnStatementSyntax)syntax).With(expressionOpt: new Renullable<ExpressionSyntax>(rhs));
        return syntax.TryWithNewRightHandSideOfAssignmentOrSingleInit(rhs);
    }
    public static SyntaxToken AsToken(this SyntaxKind kind) {
        return Syntax.Token(kind);
    }
    public static SyntaxTokenList AsTokenList(this IEnumerable<SyntaxToken> tokens) {
        return Syntax.TokenList(tokens);
    }
    public static IEnumerable<SyntaxToken> Seperators<T>(this SeparatedSyntaxList<T> list) where T : SyntaxNode {
        return list.SeparatorCount.Range().Select(e => list.GetSeparator(e));
    }
    public static SeparatedSyntaxList<T> Without<T>(this SeparatedSyntaxList<T> list, T item) where T : SyntaxNode {
        Contract.Requires(list.Contains(item));
        var i = list.IndexOf(item);
        return list.With(list.TakeSkipTake(i, 1), list.Seperators().TakeSkipTake(i, 1));
    }
}
