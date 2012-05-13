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
    internal class ConditionableIfStatement : ICodeIssueProvider {
        private readonly ICodeActionEditFactory editFactory;

        [ImportingConstructor]
        internal ConditionableIfStatement(ICodeActionEditFactory editFactory) {
            this.editFactory = editFactory;
        }

        public IEnumerable<CodeIssue> GetIssues(IDocument document, CommonSyntaxNode node, CancellationToken cancellationToken) {
            var model = document.GetSemanticModel();

            var ifNode = (IfStatementSyntax)node;
            if (ifNode.Statement.Statements().Count() != 1) return null;
            var parentBlock = ifNode.Parent as BlockSyntax;
            if (parentBlock == null) return null;
            var conditionalStatement = ifNode.Statement.Statements().Single();
            var ifNodeIndex = parentBlock.Statements.IndexOf(ifNode);

            var actions = new List<ICodeAction>();
            var trueBranchExpression = conditionalStatement.TryGetRightHandSideOfAssignmentOrSingleInitOrReturnValue();
            if (trueBranchExpression == null) return null;

            Func<ExpressionSyntax, TrivialTransforms.Placement, StatementSyntax> foldedConditional = (falseBranchExp, placement) => {
                var conditional = ifNode.Condition.Conditional(trueBranchExpression, falseBranchExp);
                return conditionalStatement
                       .TryWithNewRightHandSideOfAssignmentOrSingleInitOrReturnValue(conditional)
                       .IncludingTriviaSurrounding(ifNode, placement);
            };
            
            if (conditionalStatement.IsReturnValue()) {
                var altReturn = ifNode.ElseAndFollowingStatements().FirstOrDefault() as ReturnStatementSyntax;
                if (altReturn == null) return null;
                if (altReturn.ExpressionOpt == null) return null;
                var x = foldedConditional(altReturn.ExpressionOpt, TrivialTransforms.Placement.Around)
                        .IncludingTriviaSurrounding(altReturn, TrivialTransforms.Placement.After);

                actions.Add(new ReadyCodeAction("Fold into single return", editFactory, document, parentBlock, () => parentBlock.With(statements:
                        parentBlock.Statements.Insert(ifNodeIndex, new[] {x})
                        .Except(new StatementSyntax[] {ifNode, altReturn})
                        .List())));
            }
            if (conditionalStatement.IsAssignment()) {
                var lhs = conditionalStatement.TryGetLeftHandSideOfAssignmentOrSingleInit() as IdentifierNameSyntax;
                if (lhs == null) return null;
                
                var dataFlow = model.AnalyzeRegionDataFlow(ifNode.Condition.Span);
                if (dataFlow.ReadInside.Any(e => e.Name == lhs.PlainName)) return null;
                if (dataFlow.WrittenInside.Any(e => e.Name == lhs.PlainName)) return null;
                
                Func<StatementSyntax, ExpressionSyntax> matchingAssignment = s => {
                    if (s == null) return null;
                    
                    var lhs2 = s.TryGetLeftHandSideOfAssignmentOrSingleInit() as IdentifierNameSyntax;
                    if (lhs2 == null) return null;
                    if (lhs2.PlainName != lhs.PlainName) return null;

                    var rhs2 = s.TryGetRightHandSideOfAssignmentOrSingleInit();
                    if (rhs2 == null) return null;
                    return rhs2;
                };

                // can fold true and false statements into single statement?
                var alternativeStatement = ifNode.ElseStatementOrEmptyBlock().Statements().SingleOrDefaultAllowMany();
                var e1 = matchingAssignment(alternativeStatement);
                if (e1 != null) {
                    actions.Add(new ReadyCodeAction("Fold into single assignment", editFactory, document, ifNode, () => foldedConditional(e1, TrivialTransforms.Placement.Around)));
                }

                // can fold conditional statement into preceeding statement?
                var preceedingStatement = ifNodeIndex >= 1 ? parentBlock.Statements[ifNodeIndex - 1] : null;
                if (preceedingStatement is BlockSyntax) preceedingStatement = null;
                var e2 = matchingAssignment(preceedingStatement);
                if (ifNode.ElseOpt == null && e2 != null) {
                    var foldedAssignment = foldedConditional(e2, TrivialTransforms.Placement.After);
                    actions.Add(new ReadyCodeAction("Fold into single assignment", editFactory, document, parentBlock, () => parentBlock.With(statements:
                            parentBlock.Statements.Insert(ifNodeIndex, new[] {foldedAssignment})
                            .Except(new[] { preceedingStatement, ifNode })
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
