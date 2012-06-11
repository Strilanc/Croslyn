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
    /// <summary>Detects locals with more scope than necessary.</summary>
    [ExportSyntaxNodeCodeIssueProvider("Croslyn", LanguageNames.CSharp, typeof(LocalDeclarationStatementSyntax), typeof(ForEachStatementSyntax))]
    public class InferableType : ICodeIssueProvider {
        public IEnumerable<CodeIssue> GetIssues(IDocument document, CommonSyntaxNode node, CancellationToken cancellationToken) {
            var declaration = node as LocalDeclarationStatementSyntax;
            var loop = node as ForEachStatementSyntax;
            var model = document.GetSemanticModel();
            var assume = Assumptions.All;

            if (declaration != null) {
                foreach (var r in GetSimplifications(declaration, model, assume, cancellationToken)) {
                    yield return new CodeIssue(
                        CodeIssue.Severity.Warning,
                        r.SuggestedSpan ?? declaration.Declaration.Type.Span,
                        "Type of local variable can be infered.",
                        new[] { r.AsCodeAction(document) });
                }
            } else if (loop != null) {
                foreach (var r in GetSimplifications(loop, model, assume, cancellationToken)) {
                    yield return new CodeIssue(
                        CodeIssue.Severity.Warning,
                        r.SuggestedSpan ?? loop.Type.Span,
                        "Type of loop variable can be infered.",
                        new[] { r.AsCodeAction(document) });
                }
            }
        }
        public static IEnumerable<ReplaceAction> GetSimplifications(LocalDeclarationStatementSyntax declaration, ISemanticModel model, Assumptions assume, CancellationToken cancellationToken = default(CancellationToken)) {
            if (declaration.Declaration.Variables.Count != 1) yield break;
            var v = declaration.Declaration.Variables.Single();
            if (v.Initializer == null) yield break;
            if (declaration.Declaration.Type.IsVar) yield break;
            var declaredType = model.GetTypeInfo(declaration.Declaration.Type);
            var valueType = model.GetTypeInfo(v.Initializer.Value);
            if (!declaredType.Equals(valueType)) yield break;
            yield return new ReplaceAction("Use 'var'", declaration.Declaration.Type, Syntax.IdentifierName("var"));
        }
        public static IEnumerable<ReplaceAction> GetSimplifications(ForEachStatementSyntax loop, ISemanticModel model, Assumptions assume, CancellationToken cancellationToken = default(CancellationToken)) {
            if (loop.Type.IsVar) yield break;
            var declaredType = model.GetTypeInfo(loop.Type).Type as ITypeSymbol;
            var collectionType = model.GetTypeInfo(loop.Expression).Type as ITypeSymbol;
            if (declaredType == null || collectionType == null) yield break;
            var allInterfaces = new List<INamedTypeSymbol>();
            allInterfaces.AddRange(collectionType.AllInterfaces);
            if (collectionType is INamedTypeSymbol) allInterfaces.Add((INamedTypeSymbol)collectionType);
            var enumerableTypes = allInterfaces
                                  .Where(e => e.OriginalDefinition.SpecialType == SpecialType.System_Collections_Generic_IEnumerable_T)
                                  .ToArray();
            if (enumerableTypes.Count() != 1) yield break;
            var itemType = enumerableTypes.Single().TypeArguments.Single();

            if (!declaredType.Equals(itemType)) yield break;
            yield return new ReplaceAction("Use 'var'", loop.Type, Syntax.IdentifierName("var"));
        }

        public IEnumerable<CodeIssue> GetIssues(IDocument document, CommonSyntaxToken token, CancellationToken cancellationToken) {
            throw new NotImplementedException();
        }
        public IEnumerable<CodeIssue> GetIssues(IDocument document, CommonSyntaxTrivia trivia, CancellationToken cancellationToken) {
            throw new NotImplementedException();
        }
    }
}
