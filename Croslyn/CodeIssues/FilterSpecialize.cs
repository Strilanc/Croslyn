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
    [ExportSyntaxNodeCodeIssueProvider("Croslyn", LanguageNames.CSharp, typeof(InvocationExpressionSyntax))]
    public class FilterSpecialize : ICodeIssueProvider {
        private static readonly string[] Specializations = new[] { "First", "FirstOrDefault", "Last", "LastOrDefault", "Any" };

        public IEnumerable<CodeIssue> GetIssues(IDocument document, CommonSyntaxNode node, CancellationToken cancellationToken) {
            var invocation = (InvocationExpressionSyntax)node;
            var model = document.GetSemanticModel();
            var simplifications = GetSimplifications(invocation, model, cancellationToken);

            return simplifications.Select(e => new CodeIssue(
                CodeIssue.Severity.Warning,
                invocation.ArgumentList.Span,
                "Filter can be inlined into fold.",
                new[] { e.AsCodeAction(document) }));
        }

        public static IEnumerable<ReplaceAction> GetSimplifications(InvocationExpressionSyntax invocation, ISemanticModel model, CancellationToken cancellationToken = default(CancellationToken)) {
            var dotSpecial = invocation.Expression as MemberAccessExpressionSyntax;
            if (dotSpecial == null) yield break;
            var invokeWhere = dotSpecial.Expression as InvocationExpressionSyntax;
            if (invokeWhere == null) yield break;
            var dotWhere = invokeWhere.Expression as MemberAccessExpressionSyntax;
            if (dotWhere == null) yield break;

            if (!Specializations.Contains(dotSpecial.Name.Identifier.ValueText)) yield break;
            if (dotWhere.Name.Identifier.ValueText != "Where") yield break;
            var type = model.GetTypeInfo(dotWhere.Expression).Type;
            if (type == null) yield break;
            var specialGenericTypes = new[] { type.OriginalDefinition.SpecialType }.Concat(type.AllInterfaces.Select(e => e.OriginalDefinition.SpecialType));
            if (!specialGenericTypes.Contains(SpecialType.System_Collections_Generic_IEnumerable_T)) yield break;

            var better = invocation.WithExpression(dotWhere.WithName(dotSpecial.Name))
                                   .WithArgumentList(invokeWhere.ArgumentList);
            yield return new ReplaceAction(
                "Inline filter",
                invocation,
                better);
        }

        public IEnumerable<CodeIssue> GetIssues(IDocument document, CommonSyntaxToken token, CancellationToken cancellationToken) {
            throw new NotImplementedException();
        }
        public IEnumerable<CodeIssue> GetIssues(IDocument document, CommonSyntaxTrivia trivia, CancellationToken cancellationToken) {
            throw new NotImplementedException();
        }
    }
}
