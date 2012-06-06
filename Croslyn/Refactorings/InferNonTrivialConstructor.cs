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
    class InferNonTrivialConstructor : ICodeRefactoringProvider {
        public CodeRefactoring GetRefactoring(IDocument document, TextSpan textSpan, CancellationToken cancellationToken) {
            var tree = (SyntaxTree)document.GetSyntaxTree(cancellationToken);
            var token = tree.GetRoot().FindToken(textSpan.Start);

            if (token.Parent is ClassDeclarationSyntax || token.Parent is StructDeclarationSyntax) {
                var t = (TypeDeclarationSyntax)token.Parent;
                if (!CanInferNonTrivialConstructor(t)) return null;
                return new CodeRefactoring(new[] { new ReadyCodeAction("Infer Non-Trivial Constructor", document, t, () => {
                    var c = TryInferNonTrivialConstructor(t, document.TryGetSemanticModel());
                    var i = 0;
                    var ms = t.Members.Insert(i, new[] {c}).List();
                    return t.With(members: ms);
                })});
            }

            if (token.Parent is MemberDeclarationSyntax && (token.Parent.Parent is ClassDeclarationSyntax || token.Parent.Parent is StructDeclarationSyntax)) {
                var m = (MemberDeclarationSyntax)token.Parent;
                var t = (TypeDeclarationSyntax)m.Parent;
                if (!CanInferNonTrivialConstructor(t)) return null;
                return new CodeRefactoring(new[] { new ReadyCodeAction("Infer Non-Trivial Constructor Here", document, t, () => {
                    var c = TryInferNonTrivialConstructor(t, document.TryGetSemanticModel());
                    var i = t.Members.IndexOf(m);
                    var ms = t.Members.Insert(i, new[] {c}).List();
                    return t.With(members: ms);
                })});
            }

            return null;
        }

        public static bool CanInferNonTrivialConstructor(TypeDeclarationSyntax syntax) {
            return syntax.Members.OfType<FieldDeclarationSyntax>().Any(d => !d.IsStatic() && !d.IsReadOnly() && d.IsPublic() && d.Declaration.Variables.Any())
                || syntax.Members.OfType<FieldDeclarationSyntax>().Any(d => !d.IsStatic() && d.IsReadOnly() && d.Declaration.Variables.Any(v => v.Initializer == null))
                || syntax.Members.OfType<PropertyDeclarationSyntax>().Any(d => !d.IsStatic() && d.IsPublicSettable() && d.IsAutoProperty());
        }
        public static ConstructorDeclarationSyntax TryInferNonTrivialConstructor(TypeDeclarationSyntax syntax, ISemanticModel model = null) {
            var publicMutableFields =
                from d in syntax.Members.OfType<FieldDeclarationSyntax>()
                where !d.IsStatic()
                where !d.IsReadOnly()
                where d.IsPublic()
                from v in d.Declaration.Variables
                select new { 
                    id = v.Identifier, 
                    type = d.Declaration.Type, 
                    init = v.Initializer ?? d.Declaration.Type.NiceDefaultInitializer(model, assumeImplicitConversion: true) };

            var uninitializedReadonlyFields =
                from d in syntax.Members.OfType<FieldDeclarationSyntax>()
                where !d.IsStatic()
                where d.IsReadOnly()
                from v in d.Declaration.Variables
                where v.Initializer == null
                select new { 
                    id = v.Identifier, 
                    type = d.Declaration.Type, 
                    init = (EqualsValueClauseSyntax)null };

            var publicSetAutoProperties =
                from d in syntax.Members.OfType<PropertyDeclarationSyntax>()
                where !d.IsStatic()
                where d.IsPublicSettable()
                where d.IsAutoProperty()
                select new { 
                    id = d.Identifier, 
                    type = d.Type, 
                    init = d.Type.NiceDefaultInitializer(model, assumeImplicitConversion: true) };

            var initializables = uninitializedReadonlyFields.Concat(publicMutableFields).Concat(publicSetAutoProperties).ToArray();
            if (initializables.Length == 0) return null; //trivial

            var pars = initializables.Select(e => Syntax.Parameter(e.id).WithType(e.type).WithDefault(e.init));
            var initStatements = initializables.Select(e => Syntax.ExpressionStatement(
                Syntax.IdentifierName(Syntax.Token(SyntaxKind.ThisKeyword))
                .Accessing(Syntax.IdentifierName(e.id))
                .BOpAssigned(Syntax.IdentifierName(e.id))));
            return Syntax.ConstructorDeclaration(syntax.Identifier)
                .WithModifiers(Syntax.TokenList(Syntax.Token(SyntaxKind.PublicKeyword)))
                .WithParameterList(pars.Pars())
                .WithBody(initStatements.Block());
        }
    }
}
