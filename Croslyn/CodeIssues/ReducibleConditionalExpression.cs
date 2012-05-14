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
    internal class ReducibleConditionalExpression : ICodeIssueProvider {
        private readonly ICodeActionEditFactory editFactory;

        [ImportingConstructor]
        internal ReducibleConditionalExpression(ICodeActionEditFactory editFactory) {
            this.editFactory = editFactory;
        }

        public IEnumerable<CodeIssue> GetIssues(IDocument document, CommonSyntaxNode node, CancellationToken cancellationToken) {
            var model = document.GetSemanticModel();

            var ternaryNode = (ConditionalExpressionSyntax)node;
            if (!ternaryNode.DefinitelyHasBooleanType(model)) return null;
            var whenTrueFalseCmp = ternaryNode.WhenTrue.TryEvalAlternativeComparison(ternaryNode.WhenFalse, model);

            var actions = new List<ICodeAction>();
            if (whenTrueFalseCmp == true) {
                // (c ? b : b) --> b
                actions.Add(new ReadyCodeAction(
                    "Simplify", 
                    editFactory, 
                    document, 
                    ternaryNode, 
                    () => ternaryNode.WhenTrue));
                
                // if condition has side effects we may need to keep it
                // (c ? b : b) --> ((c && false) || b)
                if (!ternaryNode.Condition.HasSideEffects(model).IsProbablyFalse) {
                    var replacement = ternaryNode.Condition
                                      .Bracketed()
                                      .BOpLogicalAnd(false.AsLiteral())
                                      .Bracketed()
                                      .BOpLogicalOr(ternaryNode.WhenTrue);
                    actions.Add(new ReadyCodeAction(
                        "Simplify (keeping condition evaluation)", 
                        editFactory, 
                        document, 
                        ternaryNode, 
                        () => replacement));
                }
            }
            if (whenTrueFalseCmp == false) {
                // (c ? b : !b) --> (c == b)
                var replacement = ternaryNode.Condition.Bracketed().BOpEquals(ternaryNode.WhenTrue);
                actions.Add(new ReadyCodeAction(
                    "Simplify", 
                    editFactory, 
                    document, 
                    ternaryNode, 
                    () => replacement));
            }

            if (actions.Count == 0) return null;
            return new[] { new CodeIssue(
                CodeIssue.Severity.Warning, 
                ternaryNode.QuestionToken.Span, 
                "Conditional expression can be simplified into boolean expression.", 
                actions) };
        }

        public IEnumerable<CodeIssue> GetIssues(IDocument document, CommonSyntaxToken token, CancellationToken cancellationToken) {
            throw new NotImplementedException();
        }
        public IEnumerable<CodeIssue> GetIssues(IDocument document, CommonSyntaxTrivia trivia, CancellationToken cancellationToken) {
            throw new NotImplementedException();
        }
    }
}
