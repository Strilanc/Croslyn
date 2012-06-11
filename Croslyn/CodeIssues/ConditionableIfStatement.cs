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
        public IEnumerable<CodeIssue> GetIssues(IDocument document, CommonSyntaxNode node, CancellationToken cancellationToken) {
            var assume = Assumptions.All;
            var ifNode = (IfStatementSyntax)node;
            var model = document.GetSemanticModel();

            // try to get single statement branches created by the if statement's existence
            var branches = ifNode.TryGetImplicitBranchSingleStatements(model, assume);
            if (branches == null) return null;
            
            // check for same LHS/ret in both branches but not in condition
            if (!branches.True.HasMatchingLHSOrRet(branches.False, model, assume)) return null;
            var lhs = branches.True.TryGetLHSOfAssignmentOrInit(model);
            if (lhs != null) {
                var dataFlow = model.AnalyzeExpressionDataFlow(ifNode.Condition);
                if (dataFlow.ReadInside.Contains(lhs)) return null;
                if (dataFlow.WrittenInside.Contains(lhs)) return null;
            }

            // determine how to replace the if statement
            var expTrue = branches.True.TryGetRHSOfAssignmentOrInitOrReturn();
            var expFalse = branches.False.TryGetRHSOfAssignmentOrInitOrReturn();
            var expConditional = ifNode.Condition.Conditional(expTrue, expFalse);
            var replacement = branches.Base.TryUpdateRHSForAssignmentOrInitOrReturn(expConditional);

            // return as code issue / action
            var action = new ReadyCodeAction(
                "Fold into expression", 
                document, 
                new[] { ifNode, branches.True, branches.False, branches.ReplacePoint }, 
                (e, a) => e == branches.ReplacePoint ? replacement : a.Dropped());
            return action.CodeIssues1(
                CodeIssue.Severity.Warning, 
                ifNode.IfKeyword.Span, 
                "'If' statement folds into an expression");
        }

        public IEnumerable<CodeIssue> GetIssues(IDocument document, CommonSyntaxToken token, CancellationToken cancellationToken) {
            throw new NotImplementedException();
        }
        public IEnumerable<CodeIssue> GetIssues(IDocument document, CommonSyntaxTrivia trivia, CancellationToken cancellationToken) {
            throw new NotImplementedException();
        }
    }
}
