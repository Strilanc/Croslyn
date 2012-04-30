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
    class CreateEarlyReturns : ICodeRefactoringProvider {
        private readonly ICodeActionEditFactory editFactory;

        [ImportingConstructor]
        public CreateEarlyReturns(ICodeActionEditFactory editFactory) {
            this.editFactory = editFactory;
        }

        public CodeRefactoring GetRefactoring(IDocument document, TextSpan textSpan, CancellationToken cancellationToken) {
            var tree = (SyntaxTree)document.GetSyntaxTree(cancellationToken);
            var token = tree.Root.FindToken(textSpan.Start);
            var p = token.Parent;
            
            var desc = p.TryGetCodeBlockOrAreaDescription();
            if (desc == null) return null;

            return new CodeRefactoring(new[] { new ReadyCodeAction(
                "Create Early Returns",
                editFactory,
                document,
                p,
                () => p.ReplaceNodes(p.DescendentNodes().OfType<BlockSyntax>(), (e,a) => CreateEarlyReturns2(a)))});
        }
        public static BlockSyntax CreateEarlyReturns2(BlockSyntax syntax) {
            return syntax.With(statements: Syntax.List(syntax.Statements.SelectMany(e => e is IfStatementSyntax ? CreateEarlyReturns2((IfStatementSyntax)e) : new[] { e })));
        }
        public static IEnumerable<StatementSyntax> CreateEarlyReturns2(IfStatementSyntax syntax) {
            Contract.Requires(syntax != null);
            Contract.Requires(syntax.Parent is BlockSyntax);

            if (syntax.Statement.IsGuaranteedToJumpOut()) return new[] { syntax };
            if (syntax.ElseOpt != null && syntax.ElseOpt.Statement.IsGuaranteedToJumpOut()) return new[] { syntax };
            var allowedJump = syntax.TryGetEquivalentJumpAfterStatement();
            if (allowedJump == null) return new[] { syntax };

            var trueBloat = syntax.Statement.Bloat();
            var falseBloat = syntax.ElseOpt == null ? 0 : syntax.ElseOpt.Statement.Bloat();
            if (trueBloat < falseBloat * 2 - 10) 
                return new[] {
                    syntax.With(
                        statement: syntax.Statement.BracedTo(syntax.Statement.Statements().Concat(new[] {allowedJump})),
                        elseOpt: new Renullable<ElseClauseSyntax>(null))               
                }.Concat(syntax.ElseOpt.Statement.Statements());
            if (falseBloat < trueBloat * 2 - 10) 
                return new[] {
                    syntax.With(
                        condition: syntax.Condition.Inverted(),
                        statement: syntax.ElseOpt == null ? allowedJump : syntax.ElseOpt.Statement.BracedTo(syntax.ElseOpt.Statement.Statements().Concat(new[] {allowedJump})),
                        elseOpt: new Renullable<ElseClauseSyntax>(null))
                }.Concat(syntax.Statement.Statements());
            return new[] { syntax };
        }
    }
}
