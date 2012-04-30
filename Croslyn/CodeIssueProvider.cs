using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Threading;
using Roslyn.Compilers;
using Roslyn.Compilers.Common;
using Roslyn.Compilers.CSharp;
using Roslyn.Services;
using Roslyn.Services.Editor;

namespace MakeConstCS {
    [ExportSyntaxNodeCodeIssueProvider("Croslyn", LanguageNames.CSharp, typeof(ClassDeclarationSyntax), typeof(MethodDeclarationSyntax))]
    internal class CodeIssueProvider : ICodeIssueProvider {
        private readonly ICodeActionEditFactory editFactory;

        [ImportingConstructor]
        internal CodeIssueProvider(ICodeActionEditFactory editFactory) {
            this.editFactory = editFactory;
        }

        public IEnumerable<CodeIssue> GetIssues(IDocument document, CommonSyntaxNode node, CancellationToken cancellationToken) {
            return new[] { 
                GetRemoveNoOpStatements(document, node, cancellationToken), 
            }.Where(e => e != null).ToArray();
        }
        private CodeIssue GetRemoveNoOpStatements(IDocument document, CommonSyntaxNode node, CancellationToken cancellationToken) {
            return null;
            var c = node as ClassDeclarationSyntax;
            if (c == null) return null;
            if (!c.DescendentNodesAndSelf().OfType<StatementSyntax>().Any(e => e.HasSideEffects() <= Analysis.Result.FalseIfCodeFollowsConventions)) return null;
            return new CodeIssue(CodeIssue.Severity.Info, c.Identifier.Span, "Has NoOps", new[] { new ReadyCodeAction(
                "Remove NoOps",
                editFactory,
                document,
                c,
                () => c.RemoveStatementsWithNoEffect()) });
        }

        public IEnumerable<CodeIssue> GetIssues(IDocument document, CommonSyntaxToken token, CancellationToken cancellationToken) {
            throw new NotImplementedException();
        }
        public IEnumerable<CodeIssue> GetIssues(IDocument document, CommonSyntaxTrivia trivia, CancellationToken cancellationToken) {
            throw new NotImplementedException();
        }
    }
}
