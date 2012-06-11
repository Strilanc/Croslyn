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
    [ExportSyntaxNodeCodeIssueProvider("Croslyn", LanguageNames.CSharp, typeof(LocalDeclarationStatementSyntax))]
    public class OverexposedLocal : ICodeIssueProvider {
        public IEnumerable<CodeIssue> GetIssues(IDocument document, CommonSyntaxNode node, CancellationToken cancellationToken) {
            var declaration = (LocalDeclarationStatementSyntax)node;
            var model = document.GetSemanticModel();
            var assume = Assumptions.All;

            foreach (var r in GetSimplifications(declaration, model, assume, cancellationToken)) {
                yield return new CodeIssue(
                    CodeIssue.Severity.Warning,
                    r.SuggestedSpan ?? declaration.Declaration.Type.Span,
                    "Scope of local variable can be reduced.",
                    new[] { r.AsCodeAction(document) });
            }
        }
        public static IEnumerable<ReplaceAction> GetSimplifications(LocalDeclarationStatementSyntax declaration, ISemanticModel model, Assumptions assume, CancellationToken cancellationToken = default(CancellationToken)) {
            var scope = declaration.Ancestors().OfType<BlockSyntax>().First();
            var i = scope.Statements.IndexOf(declaration);
            foreach (var v in declaration.Declaration.Variables) {
                if (v.Initializer != null) {
                    if (v.Initializer.Value.HasSideEffects(model, assume) != false) continue;
                    var anyEffects = declaration.Declaration.Variables.Where(e => e.Initializer != null && e.Initializer.Value.HasSideEffects(model, assume) != false).Any();
                    if (v.Initializer.Value.IsConst(model) != true && anyEffects) continue;
                }
                var r = WithDeclarationMoved(scope, declaration, v, model, assume, cancellationToken);
                if (r == null) continue;
                var reducedDeclaration = declaration.Declaration.Variables.Count == 1
                                        ? new LocalDeclarationStatementSyntax[0]
                                        : new[] { declaration.WithDeclaration(declaration.Declaration.WithVariables(declaration.Declaration.Variables.Without(v))) };
                var newScopeWithReducedDeclaration = r.WithStatements(r.Statements.TakeSkipPutTake(i, 1, reducedDeclaration).List());
                yield return new ReplaceAction("Reduce scope", scope, newScopeWithReducedDeclaration, v.Identifier.Span);
            }
        }
        private static BlockSyntax WithDeclarationMoved(BlockSyntax scope, LocalDeclarationStatementSyntax declaration, VariableDeclaratorSyntax v, ISemanticModel model, Assumptions assume, CancellationToken cancellationToken) {
            Func<SyntaxNode, bool> accesses = s => s.DescendantNodes()
                                                    .OfType<IdentifierNameSyntax>()
                                                    .Where(e => e.Identifier.ValueText == v.Identifier.ValueText)
                                                    .Where(e => model.GetSymbolInfo(e).Symbol == model.GetDeclaredSymbol(v))
                                                    .Any();

            var isOutermostScope = scope.Statements.Contains(declaration);
            var scopeStatements = isOutermostScope
                                ? scope.Statements.SkipWhile(e => e != declaration).Skip(1).ToArray()
                                : scope.Statements.ToArray();

            var statementAccesses = scope.Statements.Where(s => accesses(s)).ToArray();
            if (statementAccesses.Length == 0) return null; //unused local, no change
            var firstAccess = statementAccesses.FirstOrDefault();
            var insertBeforeTarget = firstAccess;
            if (v.Initializer != null && v.Initializer.Value.IsConst(model) != true) {
                insertBeforeTarget = scopeStatements
                                    .TakeWhile(e => e != firstAccess)
                                    .SkipWhile(e => e.HasSideEffects(model, assume) == false)
                                    .Append(firstAccess)
                                    .FirstOrDefault();
            }

            if (statementAccesses.Length == 1 && insertBeforeTarget == firstAccess) {
                if (firstAccess is BlockSyntax) {
                    var reducedScope = WithDeclarationMoved((BlockSyntax)firstAccess, declaration, v, model, assume, cancellationToken);
                    return scope.WithStatements(scope.Statements.Replace(firstAccess, reducedScope));
                }
                if (firstAccess is IfStatementSyntax) {
                    var s = (IfStatementSyntax)firstAccess;
                    if (!accesses(s.Condition)) {
                        if (!accesses(s.Statement) && s.Else.Statement is BlockSyntax) {
                            return scope.WithStatements(
                                scope.Statements.Replace(
                                    s, 
                                    s.WithElse(
                                        s.Else.WithStatement(
                                            WithDeclarationMoved(
                                                (BlockSyntax)s.Else.Statement, 
                                                declaration, 
                                                v, 
                                                model, 
                                                assume, 
                                                cancellationToken)))));
                        } else if (s.Statement is BlockSyntax && (s.Else == null || !accesses(s.Else.Statement))) {
                            return scope.WithStatements(
                                scope.Statements.Replace(
                                    s, 
                                    s.WithStatement(
                                        WithDeclarationMoved(
                                            (BlockSyntax)s.Statement, 
                                            declaration, 
                                            v, 
                                            model, 
                                            assume, 
                                            cancellationToken))));
                        }
                    }
                }
            }
            if (isOutermostScope && scopeStatements.TakeWhile(e => e != insertBeforeTarget).Where(e => !(e is LocalDeclarationStatementSyntax)).None())
                return null; //minimal scope already
            

            var newDeclaration = declaration.WithDeclaration(declaration.Declaration.WithVariables(v.SepList1()));
            return scope.WithStatements(scope.Statements.InsertBefore(insertBeforeTarget, newDeclaration).List());
        }

        public IEnumerable<CodeIssue> GetIssues(IDocument document, CommonSyntaxToken token, CancellationToken cancellationToken) {
            throw new NotImplementedException();
        }
        public IEnumerable<CodeIssue> GetIssues(IDocument document, CommonSyntaxTrivia trivia, CancellationToken cancellationToken) {
            throw new NotImplementedException();
        }
    }
}
