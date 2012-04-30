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
                GetElseUnwrapIssue(document, node, cancellationToken), 
                GetDebracketGuardIssue(document, node, cancellationToken),
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

        private CodeIssue GetElseUnwrapIssue(IDocument document, CommonSyntaxNode node, CancellationToken cancellationToken) {
            var ifStatement = node as IfStatementSyntax;
            if (ifStatement == null || ifStatement.ElseOpt == null) return null;
            var withUnguardedElse = ifStatement.WithUnguardedElse();
            var withUnguardedElseFlip = ifStatement.Flipped().WithUnguardedElse();

            var b1 = !withUnguardedElse.SequenceEqual(new[] { ifStatement });
            var b2 = !withUnguardedElseFlip.SequenceEqual(new[] { ifStatement.Flipped() });
            if (!b1 && !b2) return null;
            return new CodeIssue(
                CodeIssue.Severity.Warning, 
                ifStatement.ElseOpt.ElseKeyword.Span, 
                "Unnecessary else",
                new[] { 
                    b1 ? new ReadyCodeAction("Inline else branch", editFactory, document, ifStatement.Parent, () => ifStatement.BracedTo(withUnguardedElse)) : null,
                    b2 ? new ReadyCodeAction("Flip and inline true branch", editFactory, document, ifStatement.Parent, () => ifStatement.BracedTo(withUnguardedElseFlip)) : null
                }.Where(e => e != null));
        }
        private CodeIssue GetDebracketGuardIssue(IDocument document, CommonSyntaxNode node, CancellationToken cancellationToken) {
            var ifStatement = node as IfStatementSyntax;
            if (ifStatement == null || ifStatement.ElseOpt != null) return null;
            var trueBlock = ifStatement.Statement as BlockSyntax;
            if (trueBlock == null) return null;
            if (trueBlock.Statements.Count != 1) return null;
            if (!trueBlock.IsGuaranteedToJumpOut()) return null;
            var r = new ReadyCodeAction("Remove braces", editFactory, document, trueBlock, () => trueBlock.Statements.Single());
            return new CodeIssue(CodeIssue.Severity.Warning, trueBlock.OpenBraceToken.Span, "Braced Guard Clause", new[] { r });
        }

        public IEnumerable<CodeIssue> GetIssues(IDocument document, CommonSyntaxToken token, CancellationToken cancellationToken) {
            throw new NotImplementedException();
        }
        public IEnumerable<CodeIssue> GetIssues(IDocument document, CommonSyntaxTrivia trivia, CancellationToken cancellationToken) {
            throw new NotImplementedException();
        }
    }
}
