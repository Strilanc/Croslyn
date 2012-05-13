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
        return Syntax.PrefixUnaryExpression(SyntaxKind.LogicalNotExpression, operand: e.Bracketed());
    }

    public static ICodeAction MakeReplaceStatementWithManyAction(this StatementSyntax oldStatement, IEnumerable<StatementSyntax> newStatements, String desc, ICodeActionEditFactory editFactory, IDocument document) {
        var statements = newStatements.ToArray();
        if (statements.Length == 1)
            return new ReadyCodeAction(desc, editFactory, document, oldStatement, () => statements.Single());
        var b = oldStatement.Parent as BlockSyntax;
        if (b != null)
            return new ReadyCodeAction(desc, editFactory, document, b, () => b.With(statements: b.Statements.WithItemReplacedByMany(oldStatement, statements)));
        return new ReadyCodeAction(desc, editFactory, document, oldStatement, () => statements.List().Block());
    }
    ///<summary>Returns an equivalent expression syntax with brackets added around it, if necessary.</summary>
    public static ParenthesizedExpressionSyntax Bracketed(this ExpressionSyntax e) {
        return e as ParenthesizedExpressionSyntax ?? Syntax.ParenthesizedExpression(expression: e);
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
        return e.With(
            condition: e.Condition.Inverted(), 
            statement: e.ElseStatementOrEmptyBlock(),
            elseOpt: (e.ElseOpt ?? Syntax.ElseClause()).With(statement: e.Statement));
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
            return (replacedNode as BlockSyntax).With(statements: newNodesIter.List());
        return newNodesIter.List().Block();
    }
    public static IEnumerable<StatementSyntax> WithUnguardedElse(this IfStatementSyntax e) {
        if (e.ElseOpt == null) return new[] { e };
        if (!e.Statement.IsGuaranteedToJumpOut()) return new[] { e };
        var trimmedIf = e.Update(e.IfKeyword, e.OpenParenToken, e.Condition, e.CloseParenToken, e.Statement, null);
        var inlinedElse = e.ElseOpt.Statement.Statements();
        var lostTrivia = e.ElseOpt.ElseKeyword.GetAllTrivia();
        return new StatementSyntax[] {
            trimmedIf.WithTrailingTrivia(trimmedIf.GetTrailingTrivia().Concat(lostTrivia))
        }.Concat(inlinedElse);
    }
        
    public static SyntaxNode NewRootForNukeMethodAndAnySideEffectsInArguments(this IDocument document, MethodDeclarationSyntax method) {
        Contract.Requires(document != null);
        Contract.Requires(method != null);
        var model = document.GetSemanticModel();
        var methodSymbol = (MethodSymbol)model.GetDeclaredSymbol(method);
        var root = ((Roslyn.Compilers.CSharp.SyntaxTree)document.GetSyntaxTree()).Root;
        var invocations = from node in root.DescendentNodes(e => !(e is ExpressionSyntax)).OfType<ExpressionStatementSyntax>()
                          let inv = node.Expression as InvocationExpressionSyntax
                          where inv != null
                          let exp = (MethodSymbol)model.GetSemanticInfo(inv.Expression).Symbol
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
        return root.ReplaceNodes(root.DescendentNodesAndSelf().OfType<StatementSyntax>().Where(e => e.HasSideEffects(model) <= Analysis.Result.FalseIfCodeFollowsConventions), (e,a) => e.Dropped());
    }

    public static StatementSyntax DropEmptyBranchesIfApplicable(this IfStatementSyntax syntax) {
        Contract.Requires(syntax != null);

        var canOmitCondition = syntax.Condition.HasSideEffects() <= Analysis.Result.FalseIfCodeFollowsConventions;
        var canOmitTrueBranch = syntax.Statement.HasSideEffects() <= Analysis.Result.FalseIfCodeFollowsConventions;
        var canOmitFalseBranch = syntax.ElseStatementOrEmptyBlock().HasSideEffects() <= Analysis.Result.FalseIfCodeFollowsConventions;

        // can we get rid of the 'if' usage?
        if (canOmitTrueBranch && canOmitFalseBranch && canOmitCondition)
            return syntax.Dropped();
        if (canOmitTrueBranch && canOmitFalseBranch)
            return Syntax.ExpressionStatement(syntax.Condition, Syntax.Token(SyntaxKind.SemicolonToken));
            
        // can we remove one of the branches?
        if (canOmitFalseBranch) 
            return syntax.With(elseOpt: new Renullable<ElseClauseSyntax>(null));
        if (canOmitTrueBranch)
            return syntax.With(
                condition: syntax.Condition.Inverted(),
                statement: syntax.ElseOpt.Statement,
                elseOpt: new Renullable<ElseClauseSyntax>(null));

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
        return node.ReplaceNodes(node.DescendentNodesAndSelf(), (e, a) => a.WithoutTrivia());
    }
}
