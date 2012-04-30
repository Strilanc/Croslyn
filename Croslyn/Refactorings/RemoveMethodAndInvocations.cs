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
    class RemoveMethodAndInvocations : ICodeRefactoringProvider {
        private readonly ICodeActionEditFactory editFactory;

        [ImportingConstructor]
        public RemoveMethodAndInvocations(ICodeActionEditFactory editFactory) {
            this.editFactory = editFactory;
        }

        public CodeRefactoring GetRefactoring(IDocument document, TextSpan textSpan, CancellationToken cancellationToken) {
            var tree = (SyntaxTree)document.GetSyntaxTree(cancellationToken);
            var root = tree.GetRoot(cancellationToken);
            var token = tree.Root.FindToken(textSpan.Start);
            var m = token.Parent as MethodDeclarationSyntax;
            if (m == null) return null;
            var model = document.TryGetSemanticModel();
            if (model == null) return null;
            
            return new CodeRefactoring(new[] { new ReadyCodeAction(
                "Remove method and calls to method (including any side-effects in arguments)", 
                editFactory, 
                document, 
                root, 
                () => document.NewRootForNukeMethodAndAnySideEffectsInArguments(m))});
        }
    }
}
