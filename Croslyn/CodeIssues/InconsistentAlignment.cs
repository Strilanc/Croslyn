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
    internal class InconsistentAlignment : ICodeIssueProvider {
        private readonly ICodeActionEditFactory editFactory;

        [ImportingConstructor]
        internal InconsistentAlignment(ICodeActionEditFactory editFactory) {
            this.editFactory = editFactory;
        }

        public IEnumerable<CodeIssue> GetIssues(IDocument document, CommonSyntaxNode node, CancellationToken cancellationToken) {
            var tree = document.TryGetSyntaxTree();
            if (tree == null) return null;
            var n = (SyntaxNode)node;
            var para = node as ParameterListSyntax;
            if (para != null) return GetIssues(document, tree, para.Parameters, n, L => para.With(parameters: L));
            var arg = node as ArgumentListSyntax;
            if (arg != null) return GetIssues(document, tree, arg.Arguments, n, L => arg.With(arguments: L));
            var ini = node as InitializerExpressionSyntax;
            if (ini != null) return GetIssues(document, tree, ini.Expressions, n, L => ini.With(expressions: L));
            return null;
        }
        private IEnumerable<CodeIssue> GetIssues<T>(IDocument document, 
                                                    CommonSyntaxTree tree,
                                                    SeparatedSyntaxList<T> list,
                                                    SyntaxNode container, 
                                                    Func<SeparatedSyntaxList<T>, SyntaxNode> containerWith) where T : SyntaxNode {
            if (container.GetText().Split('\r', '\n').Count() <= 1) return null;
            var f = list.ToDictionary(e => e, e => {
                var p = tree.GetLineSpan(e.Span, usePreprocessorDirectives: false).StartLinePosition;
                return new { n = e, line = p.Line, col = p.Character };
            });
            if (f.Values.DistinctBy(e => e.line).Count() <= 1) return null;
            if (f.Values.DistinctBy(e => e.col).Count() <= 1) return null;
            
            var correctCol = f[list.First()].col;
            var corrected = list.Select(e => e.WithExactIndentation(tree, correctCol));
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
