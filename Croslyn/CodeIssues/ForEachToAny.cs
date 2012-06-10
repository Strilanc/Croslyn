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
    [ExportSyntaxNodeCodeIssueProvider("Croslyn", LanguageNames.CSharp, typeof(ForEachStatementSyntax))]
    public class ForEachToAny : ICodeIssueProvider {
        public IEnumerable<CodeIssue> GetIssues(IDocument document, CommonSyntaxNode node, CancellationToken cancellationToken) {
            var forLoop = (ForEachStatementSyntax)node;
            var model = document.GetSemanticModel();
            var simplifications = GetSimplifications(forLoop, model, Assumptions.All, cancellationToken);
            return simplifications.Select(e => new CodeIssue(
                CodeIssue.Severity.Warning,
                forLoop.ForEachKeyword.Span,
                "'For each' loop body is idempotent.",
                new[] { e.AsCodeAction(document) }));
        }
        public static IEnumerable<ReplaceAction> GetSimplifications(ForEachStatementSyntax forLoop, ISemanticModel model, Assumptions assumptions, CancellationToken cancellationToken = default(CancellationToken)) {
            // loop body idempotent and independent of the iterator?
            if (forLoop.IsAnyIterationSufficient(model, assumptions) != true) yield break;

            // build replacement if statement, if possible
            var loopStatements = forLoop.Statement.Statements();
            if (loopStatements.None()) yield break;
            var ifBody = loopStatements.SkipLast(loopStatements.Last().IsIntraLoopJump() ? 1 : 0).Block();
            if (ifBody.HasTopLevelIntraLoopJumps()) yield break;
            var ifCondition = forLoop.Expression.Accessing("Any").Invoking();
            var rawReplacement = Syntax.IfStatement(
                condition: ifCondition,
                statement: ifBody);
            var replacement = rawReplacement.IncludingTriviaSurrounding(forLoop, TrivialTransforms.Placement.Around);

            // expose as code action/issue
            yield return new ReplaceAction(
                "Execute once if any",
                forLoop,
                replacement);
        }

        public IEnumerable<CodeIssue> GetIssues(IDocument document, CommonSyntaxToken token, CancellationToken cancellationToken) {
            throw new NotImplementedException();
        }
        public IEnumerable<CodeIssue> GetIssues(IDocument document, CommonSyntaxTrivia trivia, CancellationToken cancellationToken) {
            throw new NotImplementedException();
        }
    }
}
