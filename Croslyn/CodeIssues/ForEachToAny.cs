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
    internal class ForEachToAny : ICodeIssueProvider {
        public IEnumerable<CodeIssue> GetIssues(IDocument document, CommonSyntaxNode node, CancellationToken cancellationToken) {
            var forLoop = (ForEachStatementSyntax)node;
            var model = document.GetSemanticModel();

            // loop body idempotent and independent of the iterator?
            if (forLoop.IsAnyIterationSufficient(model, Assumptions.All) != true) return null;

            // build replacement if statement, if possible
            var loopStatements = forLoop.Statement.Statements();
            if (loopStatements.None()) return null;
            var ifBody = loopStatements.SkipLast(loopStatements.Last().IsIntraLoopJump() ? 1 : 0).Block();
            if (ifBody.HasTopLevelIntraLoopJumps()) return null;
            var ifCondition = forLoop.Expression.Accessing("Any").Invoking();
            var rawReplacement = Syntax.IfStatement(
                condition: ifCondition,
                statement: ifBody);
            var replacement = rawReplacement.IncludingTriviaSurrounding(forLoop, TrivialTransforms.Placement.Around);

            // expose as code action/issue
            var action = new ReadyCodeAction(
                "Execute once if any",
                document,
                forLoop,
                () => replacement);
            return action.CodeIssues1(
                CodeIssue.Severity.Warning,
                forLoop.ForEachKeyword.Span,
                "'For each' loop body is idempotent.");
        }

        public IEnumerable<CodeIssue> GetIssues(IDocument document, CommonSyntaxToken token, CancellationToken cancellationToken) {
            throw new NotImplementedException();
        }
        public IEnumerable<CodeIssue> GetIssues(IDocument document, CommonSyntaxTrivia trivia, CancellationToken cancellationToken) {
            throw new NotImplementedException();
        }
    }
}
