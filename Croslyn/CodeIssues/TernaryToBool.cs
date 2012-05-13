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
    [ExportSyntaxNodeCodeIssueProvider("Croslyn", LanguageNames.CSharp, typeof(ConditionalExpressionSyntax))]
    internal class TernaryToBool : ICodeIssueProvider {
        private readonly ICodeActionEditFactory editFactory;

        [ImportingConstructor]
        internal TernaryToBool(ICodeActionEditFactory editFactory) {
            this.editFactory = editFactory;
        }

        public IEnumerable<CodeIssue> GetIssues(IDocument document, CommonSyntaxNode node, CancellationToken cancellationToken) {
            var model = document.TryGetSemanticModel();
            if (model == null) return null;

            var ternaryNode = (ConditionalExpressionSyntax)node;
            if (model.GetSemanticInfo(ternaryNode).Type.SpecialType != SpecialType.System_Boolean) return null;

            var conditionNeeded = ternaryNode.Condition.HasSideEffects(model) > Analysis.Result.FalseIfCodeFollowsConventions;
            var cmp = ternaryNode.WhenTrue.TryGetAlternativeEquivalence(ternaryNode.WhenFalse, model);
            if (cmp == true) {
                var action = new ReadyCodeAction("Replace with branch", editFactory, document, ternaryNode, () => ternaryNode.WhenTrue);
                var action2 = new ReadyCodeAction("Combine branches (keeping condition evaluation)", editFactory, document, ternaryNode, () => {
                    return ternaryNode.Condition.Bracketed().BOpLogicalAnd(Syntax.LiteralExpression(SyntaxKind.FalseLiteralExpression)).Bracketed().BOpLogicalOr(ternaryNode.WhenTrue);
                });
                var actions = conditionNeeded ? new ICodeAction[] { action, action2 } : new ICodeAction[] { action };
                return new[] { new CodeIssue(CodeIssue.Severity.Warning, ternaryNode.QuestionToken.Span, "Conditional expression is equivalent to branches", actions) };
            }
            if (cmp == false) {
                var action = new ReadyCodeAction("Combine branches", editFactory, document, ternaryNode, () => {
                    return ternaryNode.Condition.Bracketed().BOpEquals(ternaryNode.WhenTrue);
                });
                return action.CodeIssues1(CodeIssue.Severity.Warning, ternaryNode.QuestionToken.Span, "Conditional expression has logically opposite branches");
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
