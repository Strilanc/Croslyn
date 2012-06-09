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
    public class ForEachFilter : ICodeIssueProvider {
        public IEnumerable<CodeIssue> GetIssues(IDocument document, CommonSyntaxNode node, CancellationToken cancellationToken) {
            var forLoop = (ForEachStatementSyntax)node;
            var model = document.TryGetSemanticModel();
            if (model == null) return null;
            var simplifications = GetSimplifications(forLoop, model, cancellationToken);

            return simplifications.Select(e => new CodeIssue(
                CodeIssue.Severity.Warning, 
                forLoop.ForEachKeyword.Span, 
                new[] {e.AsCodeAction(document)}));
        }

        public static IEnumerable<ReplaceAction> GetSimplifications(ForEachStatementSyntax forLoop, ISemanticModel model, CancellationToken cancellationToken = default(CancellationToken)) {
            var body = forLoop.Statement.Statements();
            if (body.None()) yield break;

            var unconditionalStatements = body.Skip(1);
            var conditionalStatement = body.First() as IfStatementSyntax;
            if (conditionalStatement == null) yield break;

            var trueBranch = conditionalStatement.Statement.Statements().AppendUnlessJumps(unconditionalStatements).Block();
            var falseBranch = conditionalStatement.ElseStatementOrEmptyBlock().Statements().AppendUnlessJumps(unconditionalStatements).Block();

            var falseIsEmpty = falseBranch.HasSideEffects(model).IsProbablyFalse;
            var trueIsEmpty = trueBranch.HasSideEffects(model).IsProbablyFalse;
            if (falseIsEmpty == trueIsEmpty) yield break;

            var condition = falseIsEmpty ? conditionalStatement.Condition : conditionalStatement.Condition.Inverted();
            var conditionalActions = falseIsEmpty ? trueBranch : falseBranch;

            var query = forLoop.Expression.Accessing("Where").Invoking(forLoop.Identifier.Lambdad(condition).Args1());

            var forWhereDo = forLoop.WithExpression(query).WithStatement(conditionalActions);

            yield return new ReplaceAction(
                "Filter collection",
                forLoop,
                forWhereDo);
        }

        public IEnumerable<CodeIssue> GetIssues(IDocument document, CommonSyntaxToken token, CancellationToken cancellationToken) {
            throw new NotImplementedException();
        }
        public IEnumerable<CodeIssue> GetIssues(IDocument document, CommonSyntaxTrivia trivia, CancellationToken cancellationToken) {
            throw new NotImplementedException();
        }
    }
}
