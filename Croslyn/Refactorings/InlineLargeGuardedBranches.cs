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

namespace Croslyn.Refactorings {
    [ExportCodeRefactoringProvider("Croslyn", LanguageNames.CSharp)]
    class InlineLargeGuardedBranches : ICodeRefactoringProvider {
        public CodeRefactoring GetRefactoring(IDocument document, TextSpan textSpan, CancellationToken cancellationToken) {
            var tree = (SyntaxTree)document.GetSyntaxTree(cancellationToken);
            var token = tree.GetRoot().FindToken(textSpan.Start);
            var p = token.Parent;
            
            var desc = p.TryGetCodeBlockOrAreaDescription();
            if (desc == null) return null;

            return new CodeRefactoring(new[] { new ReadyCodeAction(
                "Inline Large Guarded Branches in " + desc,
                document,
                p,
                () => p.ReplaceNodes(p.DescendantNodes().OfType<BlockSyntax>(), (e,a) => InlineLargeGuardedBranches2(a)))});
        }
        private static BlockSyntax InlineLargeGuardedBranches2(BlockSyntax syntax) {
            return syntax.With(statements: syntax.Statements.SelectMany(e => e is IfStatementSyntax ? InlineLargeGuardedBranches2(e as IfStatementSyntax) : new[] { e }).List());
        }
        private static IEnumerable<StatementSyntax> InlineLargeGuardedBranches2(IfStatementSyntax syntax) {
            Contract.Requires(syntax != null);

            var trueIsAGuard = syntax.Statement.IsGuaranteedToJumpOut();
            var falseIsAGuard = syntax.Else != null && syntax.Statement.IsGuaranteedToJumpOut();
            var preferTrue = trueIsAGuard;

            if (trueIsAGuard == falseIsAGuard && syntax.Else != null) {
                preferTrue = 2 * syntax.Statement.Bloat() >= syntax.Else.Bloat();
            }

            if (trueIsAGuard && preferTrue) return syntax.WithUnguardedElse();
            if (falseIsAGuard && !preferTrue) return syntax.Inverted().WithUnguardedElse();
            return new[] { syntax };
        }
    }
}
