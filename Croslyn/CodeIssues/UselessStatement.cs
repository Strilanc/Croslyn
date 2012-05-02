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
    [ExportSyntaxNodeCodeIssueProvider("Croslyn", LanguageNames.CSharp, typeof(StatementSyntax))]
    internal class UselessStatement : ICodeIssueProvider {
        private readonly ICodeActionEditFactory editFactory;

        [ImportingConstructor]
        internal UselessStatement(ICodeActionEditFactory editFactory) {
            this.editFactory = editFactory;
        }

        public IEnumerable<CodeIssue> GetIssues(IDocument document, CommonSyntaxNode node, CancellationToken cancellationToken) {
            var c = (StatementSyntax)node;
            if (!(c.Parent is BlockSyntax)) {
                if (c is EmptyStatementSyntax) return null;
                if (c is BlockSyntax && c.Statements().None()) return null;
            }

            if (c.HasSideEffects(document.TryGetSemanticModel()) > Analysis.Result.FalseIfCodeFollowsConventions) return null;
            return new[] { new CodeIssue(CodeIssue.Severity.Warning, c.Span, "Statement without any effect", new[] { new ReadyCodeAction(
                "Remove Unnecessary Statement",
                editFactory,
                document,
                c,
                () => c.Parent is BlockSyntax ? null : Syntax.EmptyStatement())})};
        }

        public IEnumerable<CodeIssue> GetIssues(IDocument document, CommonSyntaxToken token, CancellationToken cancellationToken) {
            throw new NotImplementedException();
        }
        public IEnumerable<CodeIssue> GetIssues(IDocument document, CommonSyntaxTrivia trivia, CancellationToken cancellationToken) {
            throw new NotImplementedException();
        }
    }
}
