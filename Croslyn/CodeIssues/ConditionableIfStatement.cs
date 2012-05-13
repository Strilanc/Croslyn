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
            var ifNode = (IfStatementSyntax)node;
            if (ifNode.Statement.Statements().Count() != 1) return null;
            var conditionalAction = ifNode.Statement.Statements().Single();

            var model = document.GetSemanticModel();
            var actions = new List<ICodeAction>();

            var s = model.GetSemanticInfo(conditionalAction.TryGetLeftHandSideOfAssignmentOrSingleInit()).Symbol;
            if (s == null) return null;
            var dataFlow = model.AnalyzeRegionDataFlow(ifNode.Condition.Span);
            if (dataFlow.ReadInside.Contains(s)) return null;
            if (dataFlow.WrittenInside.Contains(s)) return null;

            var trueBranchExpression = conditionalAction.TryGetRightHandSideOfAssignmentOrSingleInitOrReturnValue();
            if (trueBranchExpression == null) return null;

            Func<ExpressionSyntax, TrivialTransforms.Placement, StatementSyntax> foldedConditional = (falseBranchExp, placement) => {
                var conditional = ifNode.Condition.Conditional(trueBranchExpression, falseBranchExp);
                return conditionalAction
                       .TryWithNewRightHandSideOfAssignmentOrSingleInitOrReturnValue(conditional)
                       .IncludingTriviaSurrounding(ifNode, placement);
            };


            // can fold true and false statements into single statement?
            var alternativeStatement = ifNode.ElseStatementOrEmptyBlock().Statements().SingleOrDefaultAllowMany();
            if (conditionalAction.HasMatchingLHSOrRet(alternativeStatement, model)) {
                actions.Add(new ReadyCodeAction("Fold into single assignment", editFactory, document, ifNode, () => foldedConditional(alternativeStatement.TryGetRightHandSideOfAssignmentOrSingleInit(), TrivialTransforms.Placement.Around)));
            }

            var parentBlock = ifNode.Parent as BlockSyntax;
            if (parentBlock != null && ifNode.ElseOpt == null) {
                var ifNodeIndex = parentBlock.Statements.IndexOf(ifNode);

                // can fold conditional return into following return?
                if (conditionalAction.IsReturnValue()) {
                    var altReturn = ifNode.ElseAndFollowingStatements().FirstOrDefault();
                    var arhs = conditionalAction.TryGetRHSForMatchingLHSOrRet(altReturn, model);
                    if (arhs == null) return null;
                    var x = foldedConditional(arhs, TrivialTransforms.Placement.Around)
                            .IncludingTriviaSurrounding(altReturn, TrivialTransforms.Placement.After);

                    actions.Add(new ReadyCodeAction("Fold into single return", editFactory, document, parentBlock, () => parentBlock.With(statements:
                            parentBlock.Statements.Insert(ifNodeIndex, new[] { x })
                            .Except(new StatementSyntax[] { ifNode, altReturn })
                            .List())));
                }
                
                // can fold conditional assignment into preceeding assignment?
                var preceedingStatement = ifNodeIndex >= 1 ? parentBlock.Statements[ifNodeIndex - 1] : null;
                var prhs = conditionalAction.TryGetRHSForMatchingLHSOrRet(preceedingStatement, model);
                if (prhs != null && !preceedingStatement.IsGuaranteedToJumpOut()) {
                    actions.Add(new ReadyCodeAction("Fold into single assignment", editFactory, document, parentBlock, () => parentBlock.With(statements:
                            parentBlock.Statements.Insert(ifNodeIndex, new[] { foldedConditional(prhs, TrivialTransforms.Placement.After) })
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
