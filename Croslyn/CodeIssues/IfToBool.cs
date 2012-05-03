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
            var parentBlock = ifNode.Parent as BlockSyntax;
            if (parentBlock == null) return null;
            var conditionalStatement = ifNode.Statement.Statements().Single();

            var actions = new List<ICodeAction>();
            var rhs = conditionalStatement.TryGetRightHandSideOfAssignmentOrSingleInitOrReturnValue();
            if (rhs == null) return null;
            var isTrue = rhs.Kind == SyntaxKind.TrueLiteralExpression;
            var isFalse = rhs.Kind == SyntaxKind.FalseLiteralExpression;
            if (!isTrue && !isFalse) return null;
            var invertCondition = isFalse;
            var cond = invertCondition ? ifNode.Condition.Inverted() : ifNode.Condition;
            var oppKind = invertCondition ? SyntaxKind.TrueLiteralExpression : SyntaxKind.FalseLiteralExpression;

            var foldedConditional = conditionalStatement
                                    .TryWithNewRightHandSideOfAssignmentOrSingleInitOrReturnValue(cond)
                                    .IncludingTriviaSurrounding(ifNode, TrivialTransforms.Placement.Around);
            
            if (conditionalStatement.IsReturnValue()) {
                var altReturn = ifNode.ElseAndFollowingStatements().FirstOrDefault() as ReturnStatementSyntax;
                if (altReturn == null) return null;
                if (altReturn.ExpressionOpt == null) return null;
                if (altReturn.ExpressionOpt.Kind != oppKind) return null;
                var y = foldedConditional.IncludingTriviaSurrounding(altReturn, TrivialTransforms.Placement.After);

                actions.Add(new ReadyCodeAction("Fold into single return", editFactory, document, parentBlock, () => parentBlock.With(statements:
                        parentBlock.Statements.TakeWhile(e => e != ifNode)
                        .Append(foldedConditional.IncludingTriviaSurrounding(altReturn, TrivialTransforms.Placement.After))
                        .Concat(parentBlock.Statements.SkipWhile(e => e != ifNode).SkipWhile(e => e == ifNode || e == altReturn))
                        .List())));
            }
            if (conditionalStatement.IsAssignment()) {
                var lhs = conditionalStatement.TryGetLeftHandSideOfAssignmentOrSingleInit() as IdentifierNameSyntax;
                if (lhs == null) return null;
                
                var dataFlow = model.AnalyzeRegionDataFlow(ifNode.Condition.Span);
                if (dataFlow.ReadInside.Any(e => e.Name == lhs.PlainName)) return null;
                if (dataFlow.WrittenInside.Any(e => e.Name == lhs.PlainName)) return null;
                
                Func<StatementSyntax, bool> isMatchingAssignment = s => {
                    if (s == null) return false;
                    var rhs2 = s.TryGetRightHandSideOfAssignmentOrSingleInit();
                    var lhs2 = s.TryGetLeftHandSideOfAssignmentOrSingleInit() as IdentifierNameSyntax;
                    return lhs2 != null
                        && rhs2 != null
                        && lhs2.PlainName == lhs.PlainName
                        && rhs2.Kind == oppKind;
                };

                var alternativeStatement = ifNode.ElseStatementOrEmptyBlock().Statements().SingleOrDefaultAllowMany();
                var preceedingStatement = parentBlock.Statements.TakeWhile(e => e != ifNode).LastOrDefault();
                if (preceedingStatement is BlockSyntax) preceedingStatement = null;

                if (isMatchingAssignment(alternativeStatement)) {
                    actions.Add(new ReadyCodeAction("Fold into single assignment", editFactory, document, ifNode, () => foldedConditional));
                }
                if (isMatchingAssignment(preceedingStatement)) {
                    var foldedAssignment = preceedingStatement
                                           .TryWithNewRightHandSideOfAssignmentOrSingleInit(cond)
                                           .IncludingTriviaSurrounding(ifNode, TrivialTransforms.Placement.After);
                    actions.Add(new ReadyCodeAction("Fold into single assignment", editFactory, document, parentBlock, () => parentBlock.With(statements: 
                            parentBlock.Statements.TakeWhile(e => e != preceedingStatement)
                            .Append(foldedAssignment)
                            .Concat(parentBlock.Statements.SkipWhile(e => e != preceedingStatement).Skip(2))
                            .List())));
                }
            }
            if (actions.Count == 0) return null;
            return new CodeIssue[] {
                new CodeIssue(CodeIssue.Severity.Warning, ifNode.IfKeyword.Span, "'If' statement can be simplified into an expression", actions.ToArray())
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
