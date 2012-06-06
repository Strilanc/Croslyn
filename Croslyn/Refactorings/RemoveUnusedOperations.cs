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
        public CodeRefactoring GetRefactoring(IDocument document, TextSpan textSpan, CancellationToken cancellationToken) {
            var tree = (SyntaxTree)document.GetSyntaxTree(cancellationToken);
            var token = tree.GetRoot().FindToken(textSpan.Start);

            var desc = token.Parent.TryGetCodeBlockOrAreaDescription();
            if (desc == null) return null;

            return new CodeRefactoring(new[] { new ReadyCodeAction(
                "Remove Unused Operations in " + desc,
                document,
                token.Parent,
                () => token.Parent.ReplaceNodes(token.Parent.DescendantNodes().OfType<IfStatementSyntax>(), (e,a) => a.DropEmptyBranchesIfApplicable()))});
        }
    }
}
