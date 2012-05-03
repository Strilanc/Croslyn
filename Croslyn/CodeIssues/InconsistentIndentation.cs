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
    [ExportSyntaxNodeCodeIssueProvider("Croslyn", LanguageNames.CSharp, typeof(ParameterListSyntax), typeof(ArgumentListSyntax), typeof(InitializerExpressionSyntax))]
    internal class InconsistentIndentation : ICodeIssueProvider {
        private readonly ICodeActionEditFactory editFactory;

        [ImportingConstructor]
        internal InconsistentIndentation(ICodeActionEditFactory editFactory) {
            this.editFactory = editFactory;
        }

        private int? TryGetCol(SyntaxNode node) {
            var p = node;
            while (true) {
                if (node.Span.Start < p.FullSpan.Start) return null;
                var s = p.WithTrailingTrivia(new SyntaxTrivia[0]).GetFullText().Substring(0, node.Span.Start - p.FullSpan.Start);
                if (p.Parent == null || s.Contains('\r') || s.Contains('\n')) 
                    return s.Split('\r', '\n').Last().Length;
                p = p.Parent;
            }
        }
        public IEnumerable<CodeIssue> GetIssues(IDocument document, CommonSyntaxNode node, CancellationToken cancellationToken) {
            var n = (SyntaxNode)node;
            var para = node as ParameterListSyntax;
            if (para != null) return GetIssues(document, para.Parameters, n, L => para.With(parameters: L));
            var arg = node as ArgumentListSyntax;
            if (arg != null) return GetIssues(document, arg.Arguments, n, L => arg.With(arguments: L));
            var ini = node as InitializerExpressionSyntax;
            if (ini != null) return GetIssues(document, ini.Expressions, n, L => ini.With(expressions: L));
            return null;
        }
        private IEnumerable<CodeIssue> GetIssues<T>(IDocument document, 
                                                    SeparatedSyntaxList<T> list,
                                                    SyntaxNode container, 
                                                    Func<SeparatedSyntaxList<T>, SyntaxNode> containerWith) where T : SyntaxNode {
            if (container.GetText().Split('\r', '\n').Count() <= 1) return null;
            if (list.Any(e => TryGetCol(e) == null)) return null;
            var cols = list
                       .Select(e => new { n = e, c = TryGetCol(e).Value })
                       .GroupBy(e => e.c);
            if (cols.Count() <= 1) return null;
            var correctCol = TryGetCol(list.First()).Value;
            var corrected = list.Select(e => {
                var d = correctCol - TryGetCol(e).Value;
                if (d == 0) return e;
                if (d > 0) return e.WithLeadingTrivia(e.GetLeadingTrivia().Append(Syntax.Whitespace(new String(' ', d))));

                var preceedingSpace = e.GetLeadingTrivia().LastOrDefault();
                if (preceedingSpace != null && preceedingSpace.GetFullText().EndsWith(new String(' ', -d))) {
                    return e.WithLeadingTrivia(e.GetLeadingTrivia().SkipLast(1).Append(Syntax.Whitespace(new String(preceedingSpace.GetFullText().SkipLast(-d).ToArray()))));
                }
                return e.WithLeadingTrivia(e.GetLeadingTrivia().Append(Syntax.Whitespace(Environment.NewLine + new String(' ', correctCol))));
            });
            var newC = containerWith(list.With(nodes: corrected));
            var r = new ReadyCodeAction("Correct alignment", editFactory, document, container, () => newC, addFormatAnnotation: false);
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
