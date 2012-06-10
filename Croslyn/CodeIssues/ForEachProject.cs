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
    public class ForEachProject : ICodeIssueProvider {
        public IEnumerable<CodeIssue> GetIssues(IDocument document, CommonSyntaxNode node, CancellationToken cancellationToken) {
            var forLoop = (ForEachStatementSyntax)node;
            var model = document.GetSemanticModel();
            var simplifications = GetSimplifications(forLoop, model, cancellationToken);
            return simplifications.Select(e => new CodeIssue(
                CodeIssue.Severity.Warning,
                forLoop.ForEachKeyword.Span,
                "For each loop body can be simplified by projecting.",
                new[] { e.AsCodeAction(document) }));
        }
        public static IEnumerable<ReplaceAction> GetSimplifications(ForEachStatementSyntax forLoop, ISemanticModel model, CancellationToken cancellationToken = default(CancellationToken)) {
            // loop uses iterator once?
            var declaredSymbol = model.GetDeclaredSymbol(forLoop);
            var singleRead = forLoop.Statement.DescendantNodes()
                             .OfType<SimpleNameSyntax>()
                             .Where(e => model.GetSymbolInfo(e).Symbol == declaredSymbol)
                             .SingleOrDefaultAllowMany();
            if (singleRead == null) yield break;

            // safe iterator projection?
            var guaranteedProjectionPerIteration = forLoop.Statement.CompleteExecutionGuaranteesChildExecutedExactlyOnce(singleRead);
            var projection = singleRead.AncestorsAndSelf()
                             .TakeWhile(e => !(e is StatementSyntax))
                             .OfType<ExpressionSyntax>()
                             .TakeWhile(e => model.GetTypeInfo(e).Type.SpecialType != SpecialType.System_Void)
                             .TakeWhile(e => guaranteedProjectionPerIteration == true || e.HasSideEffects(model).IsProbablyFalse)
                             .LastOrDefault();
            if (projection == null) yield break;
            if (projection.TryEvalAlternativeComparison(singleRead, model) == true) yield break;

            // build replacement loop
            var projectedCollection = forLoop.Expression
                                      .Accessing("Select")
                                      .Invoking(forLoop.Identifier.Lambdad(projection).Args1());
            var newBody = forLoop.Statement.ReplaceNode(projection, singleRead);
            var replacedLoop = forLoop.WithExpression(projectedCollection).WithStatement(newBody);

            // expose as code action/issue
            yield return new ReplaceAction(
                "Project collection",
                forLoop,
                replacedLoop);
        }

        public IEnumerable<CodeIssue> GetIssues(IDocument document, CommonSyntaxToken token, CancellationToken cancellationToken) {
            throw new NotImplementedException();
        }
        public IEnumerable<CodeIssue> GetIssues(IDocument document, CommonSyntaxTrivia trivia, CancellationToken cancellationToken) {
            throw new NotImplementedException();
        }
    }
}
