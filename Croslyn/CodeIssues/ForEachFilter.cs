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
    internal class ForEachFilter : ICodeIssueProvider {
        public IEnumerable<CodeIssue> GetIssues(IDocument document, CommonSyntaxNode node, CancellationToken cancellationToken) {
            var forLoop = (ForEachStatementSyntax)node;
            var model = document.TryGetSemanticModel();
            if (model == null) return null;

            var body = forLoop.Statement.Statements();
            if (body.None()) return null;

            var unconditionalStatements = body.Skip(1);
            var conditionalStatement = body.First() as IfStatementSyntax;
            if (conditionalStatement == null) return null;

            var trueBranch = conditionalStatement.Statement.Statements().AppendUnlessJumps(unconditionalStatements).Block();
            var falseBranch = conditionalStatement.ElseStatementOrEmptyBlock().Statements().AppendUnlessJumps(unconditionalStatements).Block();

            var falseIsEmpty = falseBranch.HasSideEffects(model).IsProbablyFalse;
            var trueIsEmpty = trueBranch.HasSideEffects(model).IsProbablyFalse;
            if (falseIsEmpty == trueIsEmpty) return null;

            var condition = falseIsEmpty ? conditionalStatement.Condition : conditionalStatement.Condition.Inverted();
            var conditionalActions = falseIsEmpty ? trueBranch : falseBranch;

            var query = forLoop.Expression.Accessing("Where").Invoking(forLoop.Identifier.Lambdad(condition).Args1());

            var forWhereDo = forLoop.With(expression: query, statement: conditionalActions);

            var switchToWhere = new ReadyCodeAction(
                "Filter collection",
                document,
                forLoop,
                () => forWhereDo);

            return switchToWhere.CodeIssues1(
                CodeIssue.Severity.Warning,
                forLoop.ForEachKeyword.Span,
                "'For each' loop body is conditional.");
        }

        public IEnumerable<CodeIssue> GetIssues(IDocument document, CommonSyntaxToken token, CancellationToken cancellationToken) {
            throw new NotImplementedException();
        }
        public IEnumerable<CodeIssue> GetIssues(IDocument document, CommonSyntaxTrivia trivia, CancellationToken cancellationToken) {
            throw new NotImplementedException();
        }
    }
}
