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

            var ifNode = (IfStatementSyntax)node;
            if (ifNode.Statement.Statements().Count() != 1) return null;
            if (ifNode.ElseOpt != null && ifNode.ElseOpt.Statement.Statements().Count() != 1) return null;
            var parentBlock = ifNode.Parent as BlockSyntax;
            if (parentBlock == null) return null;
            var conditionalStatement = ifNode.Statement.Statements().Single();

            ICodeAction r = null;
            if (conditionalStatement is ReturnStatementSyntax) {
                var ret = (ReturnStatementSyntax)conditionalStatement;
                if (ret.ExpressionOpt == null) return null;
                var isTrue = ret.ExpressionOpt.Kind == SyntaxKind.TrueLiteralExpression;
                var isFalse = ret.ExpressionOpt.Kind == SyntaxKind.FalseLiteralExpression;
                if (!isTrue && !isFalse) return null;
                var invertCondition = isFalse;
                var cond = invertCondition ? ifNode.Condition.Inverted() : ifNode.Condition;
                var oppKind = invertCondition ? SyntaxKind.TrueLiteralExpression : SyntaxKind.FalseLiteralExpression;

                var alternativeNextStatementBlock = ifNode.ElseOpt != null
                                                  ? ifNode.ElseOpt.Statement
                                                  : parentBlock.Statements.SkipWhile(e => e != ifNode).Skip(1).FirstOrDefault();
                if (alternativeNextStatementBlock == null || alternativeNextStatementBlock.Statements().Count() != 1) return null;
                var alternativeReturn = alternativeNextStatementBlock.Statements().Single() as ReturnStatementSyntax;
                if (alternativeReturn == null) return null;
                if (alternativeReturn.ExpressionOpt.Kind != oppKind) return null;

                var foldedReturn = Syntax.ReturnStatement(expressionOpt: cond);
                if (ifNode.ElseOpt != null) {
                    r = new ReadyCodeAction("Fold into return condition", editFactory, document, ifNode, () => foldedReturn);
                } else {
                    r = new ReadyCodeAction("If to bool", editFactory, document, parentBlock, () => parentBlock.With(statements:
                            parentBlock.Statements.TakeWhile(e => e != ifNode)
                            .Append(foldedReturn)
                            .Concat(parentBlock.Statements.SkipWhile(e => e != ifNode).SkipWhile(e => e == ifNode || e == alternativeNextStatementBlock))
                            .List()));
                }
            }
            if (conditionalStatement is ExpressionStatementSyntax && ((ExpressionStatementSyntax)conditionalStatement).Expression.Kind == SyntaxKind.AssignExpression) {
                var alternativeStatement = ifNode.ElseOpt != null && ifNode.ElseOpt.Statement.Statements().Count() == 1
                                         ? ifNode.ElseOpt.Statement.Statements().Single() 
                                         : null;

                var binAssign = (BinaryExpressionSyntax)((ExpressionStatementSyntax)conditionalStatement).Expression;
                var target = binAssign.Left as IdentifierNameSyntax;
                if (target == null) return null;
                var result = binAssign.Right;
                var isTrue = result.Kind == SyntaxKind.TrueLiteralExpression;
                var isFalse = result.Kind == SyntaxKind.FalseLiteralExpression;
                if (!isTrue && !isFalse) return null;
                var invertCondition = isFalse;
                var cond = invertCondition ? ifNode.Condition.Inverted() : ifNode.Condition;
                var oppKind = invertCondition ? SyntaxKind.TrueLiteralExpression : SyntaxKind.FalseLiteralExpression;

                var preceedingStatement = parentBlock.Statements.TakeWhile(e => e != ifNode).LastOrDefault();
                if (preceedingStatement is BlockSyntax) preceedingStatement = null;

                Func<StatementSyntax, bool> isMatchingAssignment = s => {
                    var se = s as ExpressionStatementSyntax;
                    if (se == null) return false;
                    if (se.Expression.Kind != SyntaxKind.AssignExpression) return false;
                    var b = (BinaryExpressionSyntax)se.Expression;
                    var lhs = b.Left as IdentifierNameSyntax;
                    return lhs != null
                        && lhs.PlainName == target.PlainName
                        && b.Right.Kind == oppKind;
                };
                Func<StatementSyntax, bool> isMatchingDeclaration = s => {
                    var sd = s as LocalDeclarationStatementSyntax;
                    if (sd == null) return false;
                    if (sd.Declaration.Variables.Count() != 1) return false;
                    var v = sd.Declaration.Variables.Single();
                    return v.InitializerOpt != null
                        && v.Identifier.ValueText == target.PlainName
                        && v.InitializerOpt.Value.Kind == oppKind;
                };

                var canFoldIntoPreceedingDeclaration = isMatchingDeclaration(preceedingStatement);
                var canFoldIntoPreceedingAssignment = isMatchingAssignment(preceedingStatement);
                var canFoldIntoAlternativeAssignment = isMatchingAssignment(alternativeStatement);

                if (canFoldIntoAlternativeAssignment) {
                    r = new ReadyCodeAction("Fold into expression", editFactory, document, ifNode, () => target.varAssign(cond));
                } else if (canFoldIntoPreceedingDeclaration || canFoldIntoPreceedingAssignment) {
                    r = new ReadyCodeAction("Fold into preceeding expression", editFactory, document, parentBlock, () => parentBlock.With(statements: 
                            parentBlock.Statements.TakeWhile(e => e != preceedingStatement)
                            .Append(canFoldIntoPreceedingDeclaration ? (StatementSyntax)target.varInit(cond) : target.varAssign(cond))
                            .Concat(parentBlock.Statements.SkipWhile(e => e != preceedingStatement).Skip(2))
                            .List()));
                }
            }
            if (r == null) return null;
            return new CodeIssue[] {
                new CodeIssue(CodeIssue.Severity.Warning, ifNode.IfKeyword.Span, "'If' statement can be simplified into an expression", new[] {r})
            };
        }

        public IEnumerable<CodeIssue> GetIssues(IDocument document, CommonSyntaxToken token, CancellationToken cancellationToken) {
            throw new NotImplementedException();
        }
        public IEnumerable<CodeIssue> GetIssues(IDocument document, CommonSyntaxTrivia trivia, CancellationToken cancellationToken) {
            throw new NotImplementedException();
        }
    }
}
