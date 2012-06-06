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
    [ExportSyntaxNodeCodeIssueProvider(
        "Croslyn", 
        LanguageNames.CSharp, 
        typeof(ParameterListSyntax), 
        typeof(ArgumentListSyntax), 
        typeof(InitializerExpressionSyntax), 
        typeof(QueryExpressionSyntax))]
    internal class InconsistentAlignment : ICodeIssueProvider {
        public IEnumerable<CodeIssue> GetIssues(IDocument document, CommonSyntaxNode node, CancellationToken cancellationToken) {
            var tree = document.TryGetSyntaxTree();
            if (tree == null) return null;
            var n = (SyntaxNode)node;
            var para = node as ParameterListSyntax;
            if (para != null) return GetAlignmentIssue(document, tree, para.Parameters, n, L => para.With(parameters: para.Parameters.With(L)));
            var arg = node as ArgumentListSyntax;
            if (arg != null) return GetAlignmentIssue(document, tree, arg.Arguments, n, L => arg.With(arguments: arg.Arguments.With(L)));
            var ini = node as InitializerExpressionSyntax;
            if (ini != null) return GetAlignmentIssue(document, tree, ini.Expressions, n, L => ini.With(expressions: ini.Expressions.With(L)));
            var lin = node as QueryExpressionSyntax;
            if (lin != null) return GetAlignmentIssue(document, tree, lin.Body.Clauses, n, L => lin.WithBody(lin.Body.WithClauses(L.List())));
            return null;
        }
        private IEnumerable<CodeIssue> GetAlignmentIssue<T>(IDocument document,
                                                            CommonSyntaxTree tree,
                                                            IEnumerable<T> list,
                                                            SyntaxNode container,
                                                            Func<IEnumerable<T>, SyntaxNode> containerWith) where T : SyntaxNode {
            var pos = tree.GetLineSpan(container.Span, usePreprocessorDirectives: false);
            if (pos.StartLinePosition.Line == pos.EndLinePosition.Line) return null;

            var cols = list.Select(e => tree.GetLineSpan(e.Span, usePreprocessorDirectives: false).StartLinePosition.Character);
            if (cols.Distinct().Count() <= 1) return null;

            var alignedCol = cols.First();
            var corrected = containerWith(list.Select(e => e.WithExactIndentation(tree, alignedCol)));
            var r = new ReadyCodeAction("Correct alignment", document, container, () => corrected, addFormatAnnotation: false);
            return r.CodeIssues1(CodeIssue.Severity.Info, list.First().Span, "Inconsistent alignment");
        }

        public IEnumerable<CodeIssue> GetIssues(IDocument document, CommonSyntaxToken token, CancellationToken cancellationToken) {
            throw new NotImplementedException();
        }
        public IEnumerable<CodeIssue> GetIssues(IDocument document, CommonSyntaxTrivia trivia, CancellationToken cancellationToken) {
            throw new NotImplementedException();
        }
    }
}
