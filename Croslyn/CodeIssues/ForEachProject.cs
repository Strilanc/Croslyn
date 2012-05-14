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
    internal class ForEachProject : ICodeIssueProvider {
        private readonly ICodeActionEditFactory editFactory;

        [ImportingConstructor]
        internal ForEachProject(ICodeActionEditFactory editFactory) {
            this.editFactory = editFactory;
        }

        public IEnumerable<CodeIssue> GetIssues(IDocument document, CommonSyntaxNode node, CancellationToken cancellationToken) {
            var forLoop = (ForEachStatementSyntax)node;
            var model = document.TryGetSemanticModel();
            if (model == null) return null;

            // loop uses iterator once, projecting it?
            var sym = model.GetDeclaredSymbol(forLoop);
            var read = forLoop.Statement.DescendentNodes()
                       .OfType<SimpleNameSyntax>()
                       .Where(e => model.GetSemanticInfo(e).Symbol == sym)
                       .SingleOrDefaultAllowMany();
            if (read == null) return null;
            var projection = read.Ancestors()
                             .TakeWhile(e => e is ExpressionSyntax)
                             .Cast<ExpressionSyntax>()
                             .TakeWhile(e => e.HasSideEffects(model) <= Analysis.Result.FalseIfCodeFollowsConventions)
                             .LastOrDefault();
            if (projection == null || projection == read) return null;

            var projectedCollection = forLoop.Expression
                                      .Accessing("Select")
                                      .Invoking(forLoop.Identifier.Lambdad(projection).Args1());
            var newBody = forLoop.Statement.ReplaceNode(projection, read);
            var replacedLoop = forLoop.With(
                expression: projectedCollection,
                statement: newBody);

            var action = new ReadyCodeAction(
                "Project collection",
                editFactory,
                document,
                forLoop,
                () => replacedLoop);
            return action.CodeIssues1(
                CodeIssue.Severity.Warning,
                forLoop.ForEachKeyword.Span,
                "'For each' loop body projects items.");
        }

        public IEnumerable<CodeIssue> GetIssues(IDocument document, CommonSyntaxToken token, CancellationToken cancellationToken) {
            throw new NotImplementedException();
        }
        public IEnumerable<CodeIssue> GetIssues(IDocument document, CommonSyntaxTrivia trivia, CancellationToken cancellationToken) {
            throw new NotImplementedException();
        }
    }
}
