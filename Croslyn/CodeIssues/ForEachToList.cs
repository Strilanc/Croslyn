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
    internal class ForEachToList : ICodeIssueProvider {
        public IEnumerable<CodeIssue> GetIssues(IDocument document, CommonSyntaxNode node, CancellationToken cancellationToken) {
            var forLoop = (ForEachStatementSyntax)node;
            var model = document.TryGetSemanticModel();
            if (model == null) return null;

            // list created just before loop starts?
            var initStatement = forLoop.TryGetPrevStatement();
            var lhs = initStatement.TryGetLHSExpOfAssignmentOrInit() as IdentifierNameSyntax;
            if (lhs == null) return null;
            var target = lhs.Identifier;
            var rhs = initStatement.TryGetRHSOfAssignmentOrInit() as ObjectCreationExpressionSyntax;
            if (rhs == null) return null;
            var cr = model.GetTypeInfo(rhs).Type;
            if (cr.AllInterfaces.All(e => e.Name != "IList")) return null;

            // loop adds things directly into the list?
            var adderStatement = forLoop.Statement.Statements().SingleOrDefaultAllowMany() as ExpressionStatementSyntax;
            if (adderStatement == null) return null;
            var adderInvoke = adderStatement.Expression as InvocationExpressionSyntax;
            if (adderInvoke == null) return null;
            var adderAccess = adderInvoke.Expression as MemberAccessExpressionSyntax;
            if (adderAccess == null) return null;
            var adderTarget = adderAccess.Expression as IdentifierNameSyntax;
            if (adderTarget == null) return null;
            if (adderTarget.PlainName != target.ValueText) return null;
            if (adderAccess.Name.PlainName != "Add") return null;
            if (adderInvoke.ArgumentList.Arguments.Count != 1) return null;
            var adderExp = adderInvoke.ArgumentList.Arguments.Single().Expression as SimpleNameSyntax;
            if (adderExp == null) return null;
            if (adderExp.PlainName != forLoop.Identifier.ValueText) return null;

            var linqed = forLoop.Expression.Accessing("ToList").Invoking();
            var replacedInit = initStatement.TryWithNewRightHandSideOfAssignmentOrSingleInit(linqed);

            var action = new ReadyCodeAction(
                "Select into list",
                document,
                new[] { initStatement, forLoop },
                (e, a) => e == initStatement ? replacedInit : a.Dropped());
            return action.CodeIssues1(
                CodeIssue.Severity.Warning,
                forLoop.ForEachKeyword.Span,
                "Initializing a list with a 'for each' loop.");
        }

        public IEnumerable<CodeIssue> GetIssues(IDocument document, CommonSyntaxToken token, CancellationToken cancellationToken) {
            throw new NotImplementedException();
        }
        public IEnumerable<CodeIssue> GetIssues(IDocument document, CommonSyntaxTrivia trivia, CancellationToken cancellationToken) {
            throw new NotImplementedException();
        }
    }
}
