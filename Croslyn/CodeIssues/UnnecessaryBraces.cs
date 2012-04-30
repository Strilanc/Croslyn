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
    [ExportSyntaxNodeCodeIssueProvider("Croslyn", LanguageNames.CSharp, typeof(IfStatementSyntax))]
    internal class UnnecessaryBraces : ICodeIssueProvider {
        private readonly ICodeActionEditFactory editFactory;

        [ImportingConstructor]
        internal UnnecessaryBraces(ICodeActionEditFactory editFactory) {
            this.editFactory = editFactory;
        }

        public IEnumerable<CodeIssue> GetIssues(IDocument document, CommonSyntaxNode node, CancellationToken cancellationToken) {
            var ifStatement = node as IfStatementSyntax;
            if (ifStatement == null || ifStatement.ElseOpt != null) return null;
            var trueBlock = ifStatement.Statement as BlockSyntax;
            if (trueBlock == null) return null;
            if (trueBlock.Statements.Count != 1) return null;
            if (!trueBlock.IsGuaranteedToJumpOut()) return null;
            var r = new ReadyCodeAction("Remove unnecessary braces", editFactory, document, trueBlock, () => trueBlock.Statements.Single());

            return new[] { 
                new CodeIssue(CodeIssue.Severity.Warning, trueBlock.OpenBraceToken.Span, "Unnecessary braces", new[] { r })
            };
        }

        public IEnumerable<CodeIssue> GetIssues(IDocument document, CommonSyntaxToken token, CancellationToken cancellationToken) {
            throw new NotImplementedException();
        }
        public IEnumerable<CodeIssue> GetIssues(IDocument document, CommonSyntaxTrivia trivia, CancellationToken cancellationToken) {
            throw new NotImplementedException();
        }
    }
}
