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

            var b = conditionalAction.TryGetLeftHandSideOfAssignmentOrSingleInit();
            if (b != null) {
                var s = model.GetSemanticInfo(b).Symbol;
                if (s == null) return null;
                var dataFlow = model.AnalyzeRegionDataFlow(ifNode.Condition.Span);
                if (dataFlow.ReadInside.Contains(s)) return null;
                if (dataFlow.WrittenInside.Contains(s)) return null;
            }

            var branches = ifNode.TryGetImplicitBranchSingleStatements(model);
            if (branches == null) return null;
            if (!branches.Item1.HasMatchingLHSOrRet(branches.Item2, model)) return null;

            var conditional = ifNode.Condition.Conditional(
                branches.Item1.TryGetRightHandSideOfAssignmentOrSingleInitOrReturnValue(),
                branches.Item2.TryGetRightHandSideOfAssignmentOrSingleInitOrReturnValue());
            var @base = branches.Item1 as LocalDeclarationStatementSyntax
                     ?? branches.Item2 as LocalDeclarationStatementSyntax
                     ?? branches.Item1;
            var rep = @base.TryWithNewRightHandSideOfAssignmentOrSingleInitOrReturnValue(conditional);

            var d = new Dictionary<SyntaxNode, SyntaxNode> {
                {ifNode, rep},
                {branches.Item1, branches.Item1.Dropped()},
                {branches.Item2, branches.Item2.Dropped()}
            };
            var action = new ReadyCodeAction("Fold into conditional expression", editFactory, document, d.Keys, (e, a) => d[e]);
            return action.CodeIssues1(CodeIssue.Severity.Warning, ifNode.IfKeyword.Span, "'If' statement can be simplified into an expression");
        }

        public IEnumerable<CodeIssue> GetIssues(IDocument document, CommonSyntaxToken token, CancellationToken cancellationToken) {
            throw new NotImplementedException();
        }
        public IEnumerable<CodeIssue> GetIssues(IDocument document, CommonSyntaxTrivia trivia, CancellationToken cancellationToken) {
            throw new NotImplementedException();
        }
    }
}
