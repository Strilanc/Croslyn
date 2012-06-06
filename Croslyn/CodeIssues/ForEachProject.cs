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
        public IEnumerable<CodeIssue> GetIssues(IDocument document, CommonSyntaxNode node, CancellationToken cancellationToken) {
            var forLoop = (ForEachStatementSyntax)node;
            var model = document.GetSemanticModel();

            // loop uses iterator once?
            var declaredSymbol = model.GetDeclaredSymbol(forLoop);
            var singleRead = forLoop.Statement.DescendantNodes()
                             .OfType<SimpleNameSyntax>()
                             .Where(e => model.GetSymbolInfo(e).Symbol == declaredSymbol)
                             .SingleOrDefaultAllowMany();
            if (singleRead == null) return null;
            
            // safe iterator projection?
            var projection = singleRead.Ancestors()
                             .TakeWhile(e => e is ExpressionSyntax)
                             .Cast<ExpressionSyntax>()
                             .TakeWhile(e => e.HasSideEffects(model).IsProbablyFalse)
                             .LastOrDefault();
            if (projection == null) return null;
            if (projection.TryEvalAlternativeComparison(singleRead, model) == true) return null;

            // build replacement loop
            var projectedCollection = forLoop.Expression
                                      .Accessing("Select")
                                      .Invoking(forLoop.Identifier.Lambdad(projection).Args1());
            var newBody = forLoop.Statement.ReplaceNode(projection, singleRead);
            var replacedLoop = forLoop.WithExpression(projectedCollection).WithStatement(newBody);

            // expose as code action/issue
            var action = new ReadyCodeAction(
                "Project collection",
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
