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
    internal class UnnecessaryElse : ICodeIssueProvider {
        public IEnumerable<CodeIssue> GetIssues(IDocument document, CommonSyntaxNode node, CancellationToken cancellationToken) {
            var ifStatement = node as IfStatementSyntax;
            if (ifStatement == null || ifStatement.Else == null) return null;
            var withUnguardedElse = ifStatement.WithUnguardedElse();
            var flipped = ifStatement.Inverted();
            var withUnguardedElseFlip = flipped.WithUnguardedElse();

            var b1 = !withUnguardedElse.SequenceEqual(new[] { ifStatement });
            var b2 = !withUnguardedElseFlip.SequenceEqual(new[] { flipped });
            if (!b1 && !b2) return null;

            return new[] { new CodeIssue(
                CodeIssue.Severity.Warning,
                ifStatement.Else.ElseKeyword.Span,
                "Unnecessary else",
                new[] { 
                    b1 ? ifStatement.MakeReplaceStatementWithManyAction(withUnguardedElse, "Inline unnecessary else block", document) : null,
                    b2 ? ifStatement.MakeReplaceStatementWithManyAction(withUnguardedElseFlip, "Invert and inline unnecessary else block", document) : null
                }.Where(e => e != null)) };
        }

        public IEnumerable<CodeIssue> GetIssues(IDocument document, CommonSyntaxToken token, CancellationToken cancellationToken) {
            throw new NotImplementedException();
        }
        public IEnumerable<CodeIssue> GetIssues(IDocument document, CommonSyntaxTrivia trivia, CancellationToken cancellationToken) {
            throw new NotImplementedException();
        }
    }
}
