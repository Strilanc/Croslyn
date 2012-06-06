using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Roslyn.Compilers.CSharp;
using System.Diagnostics.Contracts;
using Roslyn.Services;
using Roslyn.Compilers.Common;
using Strilbrary.Collections;
using Strilbrary.Values;
using Roslyn.Services.Editor;

public static class Transforms {
    public static LiteralExpressionSyntax AsLiteral(this bool b) {
        return Syntax.LiteralExpression(b ? SyntaxKind.TrueLiteralExpression : SyntaxKind.FalseLiteralExpression);
    }
    public static LiteralExpressionSyntax AsLiteral(this int i) {
        return Syntax.LiteralExpression(SyntaxKind.NumericLiteralExpression, Syntax.Literal(i + "", i));
    }

    public static ExpressionSyntax MaybeInverted(this ExpressionSyntax e, bool invert) {
        return invert ? e.Inverted() : e;
    }

    ///<summary>Returns an expression syntax that is the logical inverse of the given expression syntax.</summary>
    public static ExpressionSyntax Inverted(this ExpressionSyntax e) {
        var cv = e.TryGetConstBoolValue();
        if (cv != null) cv.Value.AsLiteral();
            
        var b = e as BinaryExpressionSyntax;
        if (b != null) {
            var inverseOperators = new[] {
                Tuple.Create(SyntaxKind.EqualsEqualsToken, SyntaxKind.ExclamationEqualsToken),
                Tuple.Create(SyntaxKind.LessThanEqualsToken, SyntaxKind.GreaterThanToken),
                Tuple.Create(SyntaxKind.LessThanToken, SyntaxKind.GreaterThanEqualsToken),
            };
            foreach (var ei in inverseOperators.Concat(inverseOperators.Select(x => Tuple.Create(x.Item2, x.Item1))))
                if (b.OperatorToken.Kind == ei.Item1) 
                    return b.Update(
                        b.Left, 
                        Syntax.Token(b.OperatorToken.LeadingTrivia, ei.Item2, b.OperatorToken.TrailingTrivia), 
                        b.Right);
        }
        var u = e as PrefixUnaryExpressionSyntax;
        if (u != null && u.Kind == SyntaxKind.LogicalNotExpression) {
            return u.Operand;
        }

        var safeToExposeInside = e is LiteralExpressionSyntax
                              || e is PrefixUnaryExpressionSyntax
                              || e is IdentifierNameSyntax
                              || e is ParenthesizedExpressionSyntax
                              || e is MemberAccessExpressionSyntax
                              || e is InvocationExpressionSyntax;
        var inside = safeToExposeInside ? e : e.Bracketed();
        return Syntax.PrefixUnaryExpression(SyntaxKind.LogicalNotExpression, operand: inside).Bracketed();
    }

    public static ICodeAction MakeReplaceStatementWithManyAction(this StatementSyntax oldStatement, IEnumerable<StatementSyntax> newStatements, String desc, IDocument document) {
        var statements = newStatements.ToArray();
        if (statements.Length == 1)
            return new ReadyCodeAction(desc, document, oldStatement, () => statements.Single());
        var b = oldStatement.Parent as BlockSyntax;
        if (b != null)
            return new ReadyCodeAction(desc, document, b, () => b.WithStatements(b.Statements.WithItemReplacedByMany(oldStatement, statements)));
        return new ReadyCodeAction(desc, document, oldStatement, () => statements.List().Block());
    }
    ///<summary>Returns an equivalent expression syntax with brackets added around it, if necessary.</summary>
    public static ParenthesizedExpressionSyntax Bracketed(this ExpressionSyntax e) {
        return e as ParenthesizedExpressionSyntax ?? Syntax.ParenthesizedExpression(expression: e);
    }
    public static ExpressionSyntax BracketedOrProtected(this ExpressionSyntax e) {
        if (e is LiteralExpressionSyntax) return e;
        if (e is InvocationExpressionSyntax) return e;
        if (e is MemberAccessExpressionSyntax) return e;
        if (e is SimpleNameSyntax) return e;
        return e.Bracketed();
    }

    ///<summary>The statements in a block statement or, if not a block statement, the single statement.</summary>
    public static StatementSyntax BlockToSingleStatement(this StatementSyntax e) {
        Contract.Requires(e != null);
        Contract.Requires(e.Statements().Count() == 1);
        return e.Statements().Single();
    }

    /// <summary>Inverts the clause of an if statement, flipping the true and false branches</summary>
    public static IfStatementSyntax Inverted(this IfStatementSyntax e) {
        Contract.Requires(e != null);
        return e.WithCondition(e.Condition.Inverted())
                .WithStatement(e.ElseStatementOrEmptyBlock())
                .WithElse(e.Else == null ? Syntax.ElseClause(e.Statement) : e.Else.WithStatement(e.Statement));
    }

    public static SyntaxList<T> WithItemReplacedByMany<T>(this SyntaxList<T> items, T replacedNode, IEnumerable<T> newNodes) where T : SyntaxNode {
        Contract.Requires(items != null);
        Contract.Requires(replacedNode != null);
        Contract.Requires(newNodes != null);
        Contract.Requires(items.Contains(replacedNode));
        var i = items.IndexOf(replacedNode);
        return items.TakeSkipPutTake(i, 1, newNodes).List();
    }
    public static StatementSyntax BracedTo(this StatementSyntax replacedNode, IEnumerable<StatementSyntax> newNodesIter) {
        if (replacedNode is BlockSyntax)
            return (replacedNode as BlockSyntax).WithStatements(newNodesIter.List());
        return newNodesIter.List().Block();
    }
    public static IEnumerable<StatementSyntax> WithUnguardedElse(this IfStatementSyntax e) {
        if (e.Else == null) return new[] { e };
        if (!e.Statement.IsGuaranteedToJumpOut()) return new[] { e };
        var trimmedIf = e.Update(e.IfKeyword, e.OpenParenToken, e.Condition, e.CloseParenToken, e.Statement, null);
        var inlinedElse = e.Else.Statement.Statements();
        var lostTrivia = e.Else.ElseKeyword.GetAllTrivia();
        return new StatementSyntax[] {
            trimmedIf.WithTrailingTrivia(trimmedIf.GetTrailingTrivia().Concat(lostTrivia))
        }.Concat(inlinedElse);
    }
        
    public static SyntaxNode NewRootForNukeMethodAndAnySideEffectsInArguments(this IDocument document, MethodDeclarationSyntax method) {
        Contract.Requires(document != null);
        Contract.Requires(method != null);
        var model = document.GetSemanticModel();
        var methodSymbol = (MethodSymbol)model.GetDeclaredSymbol(method);
        var root = ((Roslyn.Compilers.CSharp.SyntaxTree)document.GetSyntaxTree()).GetRoot();
        var invocations = from node in root.DescendantNodes(e => !(e is ExpressionSyntax)).OfType<ExpressionStatementSyntax>()
                          let inv = node.Expression as InvocationExpressionSyntax
                          where inv != null
                          let exp = (MethodSymbol)model.GetSymbolInfo(inv.Expression).Symbol
                          where exp == methodSymbol
                          select (SyntaxNode)node;
        return root.ReplaceNodes(invocations.Append(method), (e, a) => e.Dropped());
    }

    /// <summary>Drops a node from its parent by replacing it with null or an empty equivalent.</summary>
    public static StatementSyntax Dropped(this StatementSyntax statement) {
        if (statement.Parent is BlockSyntax) return null;
        return Syntax.EmptyStatement(SyntaxKind.SemicolonToken.AsToken());
    }
    /// <summary>Drops a node from its parent by replacing it with null or an empty equivalent.</summary>
    public static MethodDeclarationSyntax Dropped(this MethodDeclarationSyntax method) {
        if (method.Parent is ClassDeclarationSyntax) return null;
        throw new NotImplementedException("Unrecognized MethodDeclarationSyntax parent type.");
    }
    /// <summary>Drops a node from its parent by replacing it with null or an empty equivalent.</summary>
    public static SyntaxNode Dropped(this SyntaxNode syntax) {
        var s = syntax as StatementSyntax;
        if (s != null) return s.Dropped();
        var m = syntax as MethodDeclarationSyntax;
        if (m != null) return m.Dropped();
        throw new NotImplementedException("Don't know how to drop given syntax node.");
    }

    public static SyntaxNode RemoveStatementsWithNoEffect(this SyntaxNode root, ISemanticModel model = null) {
        return root.ReplaceNodes(root.DescendantNodesAndSelf()
                                     .OfType<StatementSyntax>()
                                     .Where(e => e.HasSideEffects(model).IsProbablyFalse), 
                                 (e,a) => e.Dropped());
    }

    public static StatementSyntax DropEmptyBranchesIfApplicable(this IfStatementSyntax syntax, ISemanticModel model = null) {
        Contract.Requires(syntax != null);

        var canOmitCondition = syntax.Condition.HasSideEffects(model).IsProbablyFalse;
        var canOmitTrueBranch = syntax.Statement.HasSideEffects(model).IsProbablyFalse;
        var canOmitFalseBranch = syntax.ElseStatementOrEmptyBlock().HasSideEffects(model).IsProbablyFalse;

        // can we get rid of the 'if' usage?
        if (canOmitTrueBranch && canOmitFalseBranch && canOmitCondition)
            return syntax.Dropped();
        if (canOmitTrueBranch && canOmitFalseBranch)
            return Syntax.ExpressionStatement(syntax.Condition, Syntax.Token(SyntaxKind.SemicolonToken));
            
        // can we remove one of the branches?
        if (canOmitFalseBranch) 
            return syntax.WithElse(null);
        if (canOmitTrueBranch)
            return syntax.WithCondition(syntax.Condition.Inverted())
                         .WithStatement(syntax.Else.Statement)
                         .WithElse(null);
        return syntax;
    }

    public static T WithExactIndentation<T>(this T node, CommonSyntaxTree tree, int column) where T : SyntaxNode {
        var pos = tree.GetLineSpan(node.Span, usePreprocessorDirectives: false).StartLinePosition;
        var d = column - pos.Character;
        if (d > 0) return node.WithMoreIndentation(tree, d);
        if (d < 0) return node.WithLessIndentation(tree, -d);
        return node;
    }
    public static T WithLessIndentation<T>(this T node, CommonSyntaxTree tree, int columnDecrease) where T : SyntaxNode {
        Contract.Requires(columnDecrease >= 0);

        if (columnDecrease == 0) return node;
        var pos = tree.GetLineSpan(node.Span, usePreprocessorDirectives: false).StartLinePosition;
        columnDecrease = Math.Min(columnDecrease, pos.Character);

        var shrinker = node.GetLeadingTrivia()
                           .Where(e => e.Kind == SyntaxKind.WhitespaceTrivia)
                           .Where(e => e.GetFullText().EndsWith(new String(' ', columnDecrease)))
                           .Where(e => tree.GetLineSpan(e.FullSpan, usePreprocessorDirectives: false).StartLinePosition.Line == pos.Line)
                           .LastOrDefault();
        if (shrinker == null) {
            // there's stuff in the way of de-indentation. Just start a new line.
            var newLineSpacing = Syntax.Whitespace(Environment.NewLine + new String(' ', pos.Character - columnDecrease));
            var leading = node.GetLeadingTrivia().Append(newLineSpacing);
            return node.WithLeadingTrivia(leading);
        }
        var shrunk = Syntax.Whitespace(new String(shrinker.GetFullText().SkipLast(columnDecrease).ToArray()));
        return node.WithLeadingTrivia(node.GetLeadingTrivia().Select(e => e == shrinker ? shrunk : e));
    }
    public static T WithMoreIndentation<T>(this T node, CommonSyntaxTree tree, int columnIncrease) where T : SyntaxNode {
        Contract.Requires(columnIncrease >= 0);
        if (columnIncrease == 0) return node;
        var spacing = Syntax.Whitespace(new String(' ', columnIncrease));
        var leading = node.GetLeadingTrivia().Append(spacing);
        return node.WithLeadingTrivia(leading);
    }
    public static T WithoutTrivia<T>(this T node) where T : SyntaxNode {
        return node.WithLeadingTrivia().WithTrailingTrivia();
    }
    public static T WithoutAnyTriviaOrInternalTrivia<T>(this T node) where T : SyntaxNode {
        return node.ReplaceNodes(node.DescendantNodesAndSelf(), (e, a) => a.WithoutTrivia());
    }
}
