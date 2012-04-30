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
        private readonly ICodeActionEditFactory editFactory;

        [ImportingConstructor]
        public InferNonTrivialConstructor(ICodeActionEditFactory editFactory) {
            this.editFactory = editFactory;
        }

        public CodeRefactoring GetRefactoring(IDocument document, TextSpan textSpan, CancellationToken cancellationToken) {
            var tree = (SyntaxTree)document.GetSyntaxTree(cancellationToken);
            var token = tree.Root.FindToken(textSpan.Start);

            if (token.Parent is ClassDeclarationSyntax || token.Parent is StructDeclarationSyntax) {
                var t = (TypeDeclarationSyntax)token.Parent;
                if (!CanInferNonTrivialConstructor(t)) return null;
                return new CodeRefactoring(new[] { new ReadyCodeAction("Infer Non-Trivial Constructor", editFactory, document, t, () => {
                    var c = TryInferNonTrivialConstructor(t, document.TryGetSemanticModel());
                    var i = 0;
                    var ms = Syntax.List(t.Members.Insert(i, new[] {c}));
                    return t.With(members: ms);
                })});
            }

            if (token.Parent is MemberDeclarationSyntax && (token.Parent.Parent is ClassDeclarationSyntax || token.Parent.Parent is StructDeclarationSyntax)) {
                var m = (MemberDeclarationSyntax)token.Parent;
                var t = (TypeDeclarationSyntax)m.Parent;
                if (!CanInferNonTrivialConstructor(t)) return null;
                return new CodeRefactoring(new[] { new ReadyCodeAction("Infer Non-Trivial Constructor Here", editFactory, document, t, () => {
                    var c = TryInferNonTrivialConstructor(t, document.TryGetSemanticModel());
                    var i = t.Members.IndexOf(m);
                    var ms = Syntax.List(t.Members.Insert(i, new[] {c}));
                    return t.With(members: ms);
                })});
            }

            return null;
        }

        public static bool CanInferNonTrivialConstructor(TypeDeclarationSyntax syntax) {
            return syntax.Members.OfType<FieldDeclarationSyntax>().Any(d => !d.IsStatic() && !d.IsReadOnly() && d.IsPublic() && d.Declaration.Variables.Any())
                || syntax.Members.OfType<FieldDeclarationSyntax>().Any(d => !d.IsStatic() && d.IsReadOnly() && d.Declaration.Variables.Any(v => v.InitializerOpt == null))
                || syntax.Members.OfType<PropertyDeclarationSyntax>().Any(d => !d.IsStatic() && d.IsPublicSettable() && d.IsAutoProperty());
        }
        public static ConstructorDeclarationSyntax TryInferNonTrivialConstructor(TypeDeclarationSyntax syntax, ISemanticModel model = null) {
            var publicMutableFields =
                from d in syntax.Members.OfType<FieldDeclarationSyntax>()
                where !d.IsStatic()
                where !d.IsReadOnly()
                where d.IsPublic()
                from v in d.Declaration.Variables
                select new { id = v.Identifier, type = d.Declaration.Type, init = v.InitializerOpt ?? d.Declaration.Type.NiceDefaultInitializer(model) };

            var uninitializedReadonlyFields =
                from d in syntax.Members.OfType<FieldDeclarationSyntax>()
                where !d.IsStatic()
                where d.IsReadOnly()
                from v in d.Declaration.Variables
                where v.InitializerOpt == null
                select new { id = v.Identifier, type = d.Declaration.Type, init = (EqualsValueClauseSyntax)null };

            var publicSetAutoProperties =
                from d in syntax.Members.OfType<PropertyDeclarationSyntax>()
                where !d.IsStatic()
                where d.IsPublicSettable()
                where d.IsAutoProperty()
                select new { id = d.Identifier, type = d.Type, init = d.Type.NiceDefaultInitializer(model) };

            var initializables = uninitializedReadonlyFields.Concat(publicMutableFields).Concat(publicSetAutoProperties).ToArray();
            if (initializables.Length == 0) return null; //trivial

            var pars = initializables.Select(e => Syntax.Parameter(typeOpt: e.type, identifier: e.id, defaultOpt: e.init));
            var sets = initializables.Select(e => Syntax.ExpressionStatement(
                Syntax.BinaryExpression(
                    SyntaxKind.AssignExpression,
                    Syntax.MemberAccessExpression(
                        SyntaxKind.MemberAccessExpression,
                        Syntax.IdentifierName(Syntax.Token(SyntaxKind.ThisKeyword)),
                        Syntax.Token(SyntaxKind.DotToken),
                        Syntax.IdentifierName(e.id)),
                    Syntax.Token(SyntaxKind.EqualsToken),
                    Syntax.IdentifierName(e.id))));
            return Syntax.ConstructorDeclaration(
                identifier: syntax.Identifier,
                modifiers: Syntax.TokenList(Syntax.Token(SyntaxKind.PublicKeyword)),
                parameterList: Syntax.ParameterList(parameters: Syntax.SeparatedList(pars, Syntax.Token(SyntaxKind.CommaToken).RepeatForever().Take(pars.Count() - 1))),
                bodyOpt: Syntax.Block(statements: Syntax.List<StatementSyntax>(sets)));
        }
    }
}
