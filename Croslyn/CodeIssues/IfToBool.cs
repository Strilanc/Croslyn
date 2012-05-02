using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Threading;
using Roslyn.Compilers;
using Roslyn.Compilers.CSharp;
using Roslyn.Services;
using Roslyn.Services.Editor;
using Strilbrary.Collections;
using Roslyn.Compilers.Common;
using System.Diagnostics.Contracts;
using Strilbrary.Values;

namespace Croslyn.CodeIssues {
    [ExportSyntaxNodeCodeIssueProvider("Croslyn", LanguageNames.CSharp, typeof(IfStatementSyntax))]
    internal class IfToBool : ICodeIssueProvider {
        private readonly ICodeActionEditFactory editFactory;

        [ImportingConstructor]
        internal IfToBool(ICodeActionEditFactory editFactory) {
            this.editFactory = editFactory;
        }

        public IEnumerable<CodeIssue> GetIssues(IDocument document, CommonSyntaxNode node, CancellationToken cancellationToken) {
            var model = document.TryGetSemanticModel();
            if (model == null) return null;

            var ifBlock = (IfStatementSyntax)node;
            if (ifBlock.Statement.Statements().Count() != 1) return null;
            if (ifBlock.ElseOpt != null && ifBlock.ElseOpt.Statement.Statements().Count() != 1) return null;
            var block = ifBlock.Parent as BlockSyntax;
            if (block == null) return null;
            var trueBranch = ifBlock.Statement.Statements().Single();

            bool invertCondition;

            StatementSyntax falseNext;
            if (ifBlock.ElseOpt != null) {
                falseNext = ifBlock.ElseOpt.Statement;
            } else {
                falseNext = block.Statements.SkipWhile(e => e != ifBlock).Skip(1).FirstOrDefault();
            }
            if (falseNext == null || falseNext.Statements().Count() != 1) return null;
            var falseSingle = falseNext.Statements().Single();

            if (trueBranch is ReturnStatementSyntax) {
                var ret = (ReturnStatementSyntax)trueBranch;
                if (ret.ExpressionOpt == null) return null;
                var isTrue = ret.ExpressionOpt.Kind == SyntaxKind.TrueLiteralExpression;
                var isFalse = ret.ExpressionOpt.Kind == SyntaxKind.FalseLiteralExpression;
                if (!isTrue && !isFalse) return null;
                invertCondition = isFalse;

                var fret = falseSingle as ReturnStatementSyntax;
                if (fret == null) return null;
                if (fret.ExpressionOpt.Kind != (isTrue ? SyntaxKind.FalseLiteralExpression : SyntaxKind.TrueLiteralExpression)) return null;

                var cond = isTrue ? ifBlock.Condition : ifBlock.Condition.Inverted();
                var ns = Syntax.ReturnStatement(expressionOpt: cond);
                var pre = block.Statements.TakeWhile(e => e != ifBlock);
                var post = block.Statements.SkipWhile(e => e != ifBlock).SkipWhile(e => e == ifBlock || e == falseNext);
                var nb = block.With(statements: pre.Append(ns).Concat(post).List());
                ICodeAction r;
                if (ifBlock.ElseOpt != null)
                    r = new ReadyCodeAction("If to bool", editFactory, document, ifBlock, () => ns);
                else
                    r = new ReadyCodeAction("If to bool", editFactory, document, block, () => nb);
                return new CodeIssue[] {
                    new CodeIssue(CodeIssue.Severity.Warning, ifBlock.IfKeyword.Span, new[] {r})
                };
            }
            if (trueBranch is ExpressionStatementSyntax && ((ExpressionStatementSyntax)trueBranch).Expression.Kind == SyntaxKind.AssignExpression) {
                var bet = (BinaryExpressionSyntax)((ExpressionStatementSyntax)trueBranch).Expression;
                var net = bet.Left as IdentifierNameSyntax;
                if (net == null) return null;
                var isTrue = bet.Right.Kind == SyntaxKind.TrueLiteralExpression;
                var isFalse = bet.Right.Kind == SyntaxKind.FalseLiteralExpression;
                invertCondition = isFalse;
                if (!isTrue && !isFalse) return null;

                var pre = block.Statements.TakeWhile(e => e != ifBlock).LastOrDefault();
                if (pre != null && pre.Statements().Count() == 1) pre = pre.Statements().Single();
                var doPostAssign = false;
                var doPreDecl = false;
                var doPreAssign = false;
                if (pre is LocalDeclarationStatementSyntax) {
                    var x = (LocalDeclarationStatementSyntax)pre;
                    if (x.Declaration.Variables.Count() == 1) {
                        var v = x.Declaration.Variables.Single();
                        if (v.InitializerOpt != null && v.Identifier.ValueText == net.PlainName && v.InitializerOpt.Value.Kind == (isTrue ? SyntaxKind.FalseLiteralExpression : SyntaxKind.TrueLiteralExpression)) {
                            doPreDecl = true;
                        }
                    }
                } else if (pre is ExpressionStatementSyntax) {
                    if (pre is ExpressionStatementSyntax && ((ExpressionStatementSyntax)pre).Expression.Kind == SyntaxKind.AssignExpression) {
                        var fret2 = (BinaryExpressionSyntax)((ExpressionStatementSyntax)pre).Expression;
                        doPreAssign = fret2.Left is IdentifierNameSyntax
                                      && net.PlainName == ((IdentifierNameSyntax)fret2.Left).PlainName
                                      && fret2.Right.Kind == (isTrue ? SyntaxKind.FalseLiteralExpression : SyntaxKind.TrueLiteralExpression);
                    }
                }
                if (falseSingle is ExpressionStatementSyntax && ((ExpressionStatementSyntax)falseSingle).Expression.Kind == SyntaxKind.AssignExpression) {
                    var fret2 = (BinaryExpressionSyntax)((ExpressionStatementSyntax)falseSingle).Expression;
                    doPostAssign = fret2.Left is IdentifierNameSyntax
                                   && net.PlainName == ((IdentifierNameSyntax)fret2.Left).PlainName
                                   && fret2.Right.Kind == (isTrue ? SyntaxKind.FalseLiteralExpression : SyntaxKind.TrueLiteralExpression);
                }
                if (!doPostAssign && !doPreDecl && !doPreAssign) return null;

                var cond = invertCondition ? ifBlock.Condition.Inverted() : ifBlock.Condition;
                var ns = doPreDecl 
                         ? (StatementSyntax)Syntax.Identifier(net.PlainName).varInit(cond)
                         : Syntax.ExpressionStatement(Syntax.BinaryExpression(SyntaxKind.AssignExpression, net, Syntax.Token(SyntaxKind.EqualsToken), cond));
                var prex = block.Statements.TakeWhile(e => e != ifBlock && (doPostAssign || e != pre));
                var post = block.Statements.SkipWhile(e => e != ifBlock && (doPostAssign || e != pre)).SkipWhile(e => e == pre || e == ifBlock || e == falseNext);
                var nb = block.With(statements: prex.Append(ns).Concat(post).List());
                ICodeAction r;
                if (ifBlock.ElseOpt != null)
                    r = new ReadyCodeAction("If to bool", editFactory, document, ifBlock, () => ns);
                else
                    r = new ReadyCodeAction("If to bool", editFactory, document, block, () => nb);
                return new CodeIssue[] {
                    new CodeIssue(CodeIssue.Severity.Warning, ifBlock.IfKeyword.Span, new[] {r})
                };
            }
            return null;
        }

        public IEnumerable<CodeIssue> GetIssues(IDocument document, CommonSyntaxToken token, CancellationToken cancellationToken) {
            throw new NotImplementedException();
        }
        public IEnumerable<CodeIssue> GetIssues(IDocument document, CommonSyntaxTrivia trivia, CancellationToken cancellationToken) {
            throw new NotImplementedException();
        }
    }
}
