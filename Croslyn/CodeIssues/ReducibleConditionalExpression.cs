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
    public class ReducibleConditionalExpression : ICodeIssueProvider {
        public IEnumerable<CodeIssue> GetIssues(IDocument document, CommonSyntaxNode node, CancellationToken cancellationToken) {
            var model = document.GetSemanticModel();
            var ternaryNode = (ConditionalExpressionSyntax)node;
            var actions = GetSimplifications(ternaryNode, model, cancellationToken)
                          .Select(e => e.AsCodeAction(document))
                          .ToArray();
            if (actions.Length == 0) yield break;
               
            yield return new CodeIssue(
                CodeIssue.Severity.Warning, 
                ternaryNode.QuestionToken.Span, 
                "Conditional expression simplifies into boolean expression.", 
                actions);
        }

        public static IEnumerable<ReplaceAction> GetSimplifications(ConditionalExpressionSyntax ternaryNode, ISemanticModel model, CancellationToken cancellationToken = default(CancellationToken)) {
            if (!ternaryNode.DefinitelyHasBooleanType(model)) yield break;
            var whenTrueFalseCmp = ternaryNode.WhenTrue.TryEvalAlternativeComparison(ternaryNode.WhenFalse, model);

            if (whenTrueFalseCmp == true) {
                // (c ? b : b) --> b
                yield return new ReplaceAction(
                    "Simplify",
                    ternaryNode,
                    ternaryNode.WhenTrue);

                // if condition has side effects we may need to keep it
                // (c ? b : b) --> ((c && false) || b)
                if (!ternaryNode.Condition.HasSideEffects(model).IsProbablyFalse) {
                    var replacement = ternaryNode.Condition
                                      .BracketedOrProtected()
                                      .BOpLogicalAnd(false.AsLiteral())
                                      .Bracketed()
                                      .BOpLogicalOr(ternaryNode.WhenTrue);
                    yield return new ReplaceAction(
                        "Simplify (keeping condition evaluation)",
                        ternaryNode,
                        replacement);
                }
            }
            if (whenTrueFalseCmp == false) {
                // (c ? b : !b) --> (c == b)
                var replacement = ternaryNode.Condition
                                  .BracketedOrProtected()
                                  .BOpEquals(ternaryNode.WhenTrue);
                yield return new ReplaceAction(
                    "Simplify",
                    ternaryNode,
                    replacement);
            }
        }

        public IEnumerable<CodeIssue> GetIssues(IDocument document, CommonSyntaxToken token, CancellationToken cancellationToken) {
            throw new NotImplementedException();
        }
        public IEnumerable<CodeIssue> GetIssues(IDocument document, CommonSyntaxTrivia trivia, CancellationToken cancellationToken) {
            throw new NotImplementedException();
        }
    }
}
