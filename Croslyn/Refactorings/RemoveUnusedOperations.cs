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

namespace Croslyn.Refactorings {
    [ExportCodeRefactoringProvider("Croslyn", LanguageNames.CSharp)]
    class RemoveUnusedOperations : ICodeRefactoringProvider {
        private readonly ICodeActionEditFactory editFactory;

        [ImportingConstructor]
        public RemoveUnusedOperations(ICodeActionEditFactory editFactory) {
            this.editFactory = editFactory;
        }

        public CodeRefactoring GetRefactoring(IDocument document, TextSpan textSpan, CancellationToken cancellationToken) {
            var tree = (SyntaxTree)document.GetSyntaxTree(cancellationToken);
            var token = tree.Root.FindToken(textSpan.Start);

            var desc = token.Parent.TryGetCodeBlockOrAreaDescription();
            if (desc == null) return null;

            return new CodeRefactoring(new[] { new ReadyCodeAction(
                "Remove Unused Operations in " + desc,
                editFactory,
                document,
                token.Parent,
                () => token.Parent.ReplaceNodes(token.Parent.DescendentNodes().OfType<IfStatementSyntax>(), (e,a) => a.DropEmptyBranchesIfApplicable()))});
        }
    }
}
