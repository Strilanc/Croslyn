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
    internal class ForEachToSelect : ICodeIssueProvider {
        private readonly ICodeActionEditFactory editFactory;

        [ImportingConstructor]
        internal ForEachToSelect(ICodeActionEditFactory editFactory) {
            this.editFactory = editFactory;
        }

        public IEnumerable<CodeIssue> GetIssues(IDocument document, CommonSyntaxNode node, CancellationToken cancellationToken) {
            var forLoop = (ForEachStatementSyntax)node;
            var model = document.TryGetSemanticModel();
            if (model == null) return null;

            var parent = forLoop.Parent as BlockSyntax;
            if (parent == null) return null;
            var preceedingStatement = parent.Statements.TakeWhile(e => e != forLoop).TakeLast(1).SingleOrDefault();
            if (!preceedingStatement.IsAssignmentOrSingleInitialization()) return null;
            var lhs = preceedingStatement.TryGetLeftHandSideOfAssignmentOrSingleInit() as IdentifierNameSyntax;
            var rhs = preceedingStatement.TryGetRightHandSideOfAssignmentOrSingleInit();
            if (lhs == null) return null;
            var target = lhs.Identifier;
            var cre = rhs as ObjectCreationExpressionSyntax;
            if (cre == null) return null;
            var cr = model.GetSemanticInfo(cre).Type;
            if (cr.AllInterfaces.All(e => e.Name != "IList")) return null;

            var action = forLoop.Statement;
            if (action.Statements().Count() == 1) action = action.Statements().Single();
            var adderStatement = action as ExpressionStatementSyntax;
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
            var adderExp = adderInvoke.ArgumentList.Arguments.Single().Expression;

            var linqed = forLoop.Expression
                         .Accessing("Select")
                         .Invoking(forLoop.Identifier.Lambdad(adderExp).Args1())
                         .Accessing("ToList")
                         .Invoking();
            var newPreceedingStatement = preceedingStatement.TryWithNewRightHandSideOfAssignmentOrSingleInit(linqed);

            var toIfAnyThenAction = new ReadyCodeAction(
                "for(x){add(y)} -> =x.Select(y).ToList()",
                editFactory,
                document,
                parent,
                () => parent.With(statements: 
                    parent.Statements.TakeWhile(e => e != preceedingStatement)
                    .Append(newPreceedingStatement)
                    .Concat(parent.Statements.SkipWhile(e => e != preceedingStatement).Skip(2))
                    .List()));

            return toIfAnyThenAction.CodeIssues1(
                CodeIssue.Severity.Warning,
                forLoop.ForEachKeyword.Span,
                "Populating a list with a loop instead of a Linq query.");
        }

        public IEnumerable<CodeIssue> GetIssues(IDocument document, CommonSyntaxToken token, CancellationToken cancellationToken) {
            throw new NotImplementedException();
        }
        public IEnumerable<CodeIssue> GetIssues(IDocument document, CommonSyntaxTrivia trivia, CancellationToken cancellationToken) {
            throw new NotImplementedException();
        }
    }
}
