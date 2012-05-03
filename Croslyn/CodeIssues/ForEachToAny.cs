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
        private readonly ICodeActionEditFactory editFactory;

        [ImportingConstructor]
        internal ForEachToAny(ICodeActionEditFactory editFactory) {
            this.editFactory = editFactory;
        }

        public IEnumerable<CodeIssue> GetIssues(IDocument document, CommonSyntaxNode node, CancellationToken cancellationToken) {
            var forLoop = (ForEachStatementSyntax)node;
            var model = document.TryGetSemanticModel();
            if (model == null) return null;

            var body = forLoop.Statement.Statements();
            if (body.None()) return null;

            var bodyWithoutJump = body.SkipLast(body.Last().IsIntraLoopJump() ? 1 : 0);
            if (bodyWithoutJump.Any(e => e.HasTopLevelIntraLoopJumps())) return null;

            var iteratorReads = forLoop.Statement.ReadsOfLocalVariable(forLoop.Identifier).ToArray();
            if (iteratorReads.Length > 0) return null;
            if (forLoop.Statement.IsLoopVarFirstpotent(iteratorReads, model) < Analysis.Result.TrueIfCodeFollowsConventions) return null;

            var ifAnyStatement = Syntax.IfStatement(
                condition: forLoop.Expression.Accessing("Any").Invoking(),
                statement: bodyWithoutJump.Block());

            var toIfAnyStatement = new ReadyCodeAction(
                "for(c){a} -> if(c.Any()){a}",
                editFactory,
                document,
                forLoop,
                () => ifAnyStatement.IncludingTriviaSurrounding(forLoop, TrivialTransforms.Placement.Around));

            return toIfAnyStatement.CodeIssues1(
                CodeIssue.Severity.Warning,
                forLoop.ForEachKeyword.Span,
                "Loop body is idempotent.");
        }

        public IEnumerable<CodeIssue> GetIssues(IDocument document, CommonSyntaxToken token, CancellationToken cancellationToken) {
            throw new NotImplementedException();
        }
        public IEnumerable<CodeIssue> GetIssues(IDocument document, CommonSyntaxTrivia trivia, CancellationToken cancellationToken) {
            throw new NotImplementedException();
        }
    }
}
