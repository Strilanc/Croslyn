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
using LinqToCollections.Set;

namespace Croslyn.CodeIssues {
    [ExportSyntaxNodeCodeIssueProvider("Croslyn", LanguageNames.CSharp, typeof(FieldDeclarationSyntax))]
    internal class UnnecessaryMutability : ICodeIssueProvider {
        public IEnumerable<CodeIssue> GetIssues(IDocument document, CommonSyntaxNode node, CancellationToken cancellationToken) {
            var model = document.TryGetSemanticModel();
            if (model == null) return null;
            if (model.GetDiagnostics().Any(e => e.Info.Severity == DiagnosticSeverity.Error)) return null;
            if (document.Project.Documents.Any(e => e.GetSyntaxTree() == null || e.GetSemanticModel() == null)) return null;

            var fieldNode = (FieldDeclarationSyntax)node;
            if (fieldNode.IsReadOnly()) return null;

            var classDecl = fieldNode.Ancestors().OfType<ClassDeclarationSyntax>().FirstOrDefault();
            if (classDecl == null) return null;
            var constructors = classDecl.Members.OfType<ConstructorDeclarationSyntax>().ToRet();

            var modsWithReadOnly = fieldNode.Modifiers.Append(SyntaxKind.ReadOnlyKeyword.AsToken()).AsTokenList();

            var scopes = fieldNode.IsPrivate()
                       ? new[] {Tuple.Create((CommonSyntaxNode)classDecl, model)}
                       : document.Project.Documents.Select(e => Tuple.Create(e.GetSyntaxTree().GetRoot(), e.GetSemanticModel()));
            
            var unmutatedVars = fieldNode.Declaration.Variables.Where(v => {
                var field = model.GetDeclaredSymbol(v);
                if (scopes.Any(c => c.Item1.DescendantNodes(e => !constructors.Contains(e)).OfType<ExpressionSyntax>().Any(e => SurfaceWritesTo(e, c.Item2, field)))) return false;
                return true;
            }).ToArray();

            if (unmutatedVars.Length == 0) return null;
            if (unmutatedVars.Length == fieldNode.Declaration.Variables.Count) {
                var r = new ReadyCodeAction("Make readonly", document, fieldNode, () => fieldNode.WithModifiers(modsWithReadOnly));
                var desc = unmutatedVars.Length == 1 ? "Mutable field is never modified." : "Mutable fields are never modified.";
                return r.CodeIssues1(CodeIssue.Severity.Warning, fieldNode.Declaration.Type.Span, desc);
            }

            return unmutatedVars.Select(v => {
                var singleReadOnly = fieldNode.WithModifiers(modsWithReadOnly)
                                              .WithDeclaration(fieldNode.Declaration.WithVariables(v.SepList1()));
                var rest = fieldNode
                           .WithLeadingTrivia()
                           .WithTrailingTrivia(Syntax.Whitespace(Environment.NewLine))
                           .WithDeclaration(fieldNode.Declaration.WithVariables(fieldNode.Declaration.Variables.Without(v)));
                var newClassDecl = classDecl.With(members: classDecl.Members.WithItemReplacedByMany(fieldNode, new[] { singleReadOnly, rest }));
                var action = new ReadyCodeAction("Split readonly", document, classDecl, () => newClassDecl);
                return new CodeIssue(CodeIssue.Severity.Warning, v.Identifier.Span, "Mutable field is never modified.", new[] { action });
            }).ToArray();
        }
        private bool SurfaceWritesTo(ExpressionSyntax expression, ISemanticModel model, ISymbol target) {
            var r = expression as InvocationExpressionSyntax;
            if (r != null) {
                return r.ArgumentList.Arguments
                    .Where(e => e.RefOrOutKeyword != null)
                    .Select(e => model.GetSymbolInfo(e))
                    .Where(e => e.Symbol == target || e.CandidateSymbols.Contains(target))
                    .Any();
            }
            if (Analysis.AssignmentOperatorKinds.Contains(expression.Kind)) {
                var i = model.GetSymbolInfo(((BinaryExpressionSyntax)expression).Left);
                return i.Symbol == target || i.CandidateSymbols.Contains(target);
            }
            return false;
        }

        public IEnumerable<CodeIssue> GetIssues(IDocument document, CommonSyntaxToken token, CancellationToken cancellationToken) {
            throw new NotImplementedException();
        }
        public IEnumerable<CodeIssue> GetIssues(IDocument document, CommonSyntaxTrivia trivia, CancellationToken cancellationToken) {
            throw new NotImplementedException();
        }
    }
}
