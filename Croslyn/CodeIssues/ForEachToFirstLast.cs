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
    [ExportSyntaxNodeCodeIssueProvider("Croslyn", LanguageNames.CSharp, typeof(ForEachStatementSyntax))]
    internal class ForEachToFirstLast : ICodeIssueProvider {
        private readonly ICodeActionEditFactory editFactory;

        [ImportingConstructor]
        internal ForEachToFirstLast(ICodeActionEditFactory editFactory) {
            this.editFactory = editFactory;
        }

        public IEnumerable<CodeIssue> GetIssues(IDocument document, CommonSyntaxNode node, CancellationToken cancellationToken) {
            var forLoop = (ForEachStatementSyntax)node;
            var model = document.GetSemanticModel();

            // can the loop be replaced by its first or last iteration?
            var body = forLoop.Statement.Statements();
            if (body.None()) return null;
            var loopStatements = body.SkipLast(body.Last().IsIntraLoopJump() ? 1 : 0);
            if (loopStatements.Any(e => e.HasTopLevelIntraLoopJumps())) return null;
            var iteratorReads = forLoop.Statement.ReadsOfLocalVariable(forLoop.Identifier).ToArray();
            if (iteratorReads.Length == 0) return null;
            var isFirstSufficient = forLoop.Statement.IsLoopVarFirstpotent(model, iteratorReads) >= Analysis.Result.TrueIfCodeFollowsConventions;
            var isLastSufficient = forLoop.Statement.IsLoopVarLastpotent(model, iteratorReads) >= Analysis.Result.TrueIfCodeFollowsConventions;
            var firstVsLast = isFirstSufficient ? "First"
                            : isLastSufficient ? "Last"
                            : null;
            if (firstVsLast == null) return null;

            // using a nullable type (so that a null result definitively indicates an empty collection)
            var iteratorType = ((LocalSymbol)model.GetDeclaredSymbol(forLoop)).Type;
            var nuller = GetNullabledQueryAndValueGetter(iteratorType, forLoop.Identifier, forLoop.Expression);
            var nullableQuery = nuller.Item1;
            var valueGetter = nuller.Item2;
            var tempNullableLocalName = Syntax.Identifier("_" + forLoop.Identifier.ValueText);
            var tempNullableLocalGet = Syntax.IdentifierName(tempNullableLocalName);

            // build replacement
            var desiredIterationQuery = nullableQuery.Accessing(firstVsLast + "OrDefault").Invoking();
            var condition = tempNullableLocalGet.BOpNotEquals(Syntax.LiteralExpression(SyntaxKind.NullLiteralExpression));
            var useDenulledLocal = iteratorReads.Length > 2;
            var thenStatement = useDenulledLocal
                              ? loopStatements.Prepend(forLoop.Identifier.VarInit(valueGetter(tempNullableLocalGet))).Block()
                              : loopStatements.Select(e => e.ReplaceNodes(iteratorReads, (n, a) => valueGetter(tempNullableLocalGet))).Block();
            var replacementStatements = new StatementSyntax[] {
                tempNullableLocalName.VarInit(desiredIterationQuery),
                condition.IfThen(thenStatement)
            };

            // expose as code action/issue
            var action = forLoop.MakeReplaceStatementWithManyAction(
                replacementStatements,
                "Execute " + firstVsLast + " if any",
                editFactory,
                document);
            return action.CodeIssues1(
                CodeIssue.Severity.Warning,
                forLoop.ForEachKeyword.Span,
                firstVsLast + " execution of 'for each' loop body is sufficient.");
        }
        public static Tuple<ExpressionSyntax, Func<IdentifierNameSyntax, ExpressionSyntax>> GetNullabledQueryAndValueGetter(TypeSymbol itemType, SyntaxToken iterator, ExpressionSyntax collection) {
            if (!itemType.IsReferenceType && itemType.SpecialType != SpecialType.System_Nullable_T) {
                //wrap it in Nullable<T>
                return Tuple.Create<ExpressionSyntax, Func<IdentifierNameSyntax, ExpressionSyntax>>(
                    collection.Accessing(Syntax.Identifier("Cast").Genericed(itemType.Name.AsIdentifier().Nullable())).Invoking(),
                    e => e.Accessing("Value"));
            }

            //wrap it in Tuple<T>
            var wrappedRead = Syntax.IdentifierName("Tuple").Accessing("Create").Invoking(Syntax.IdentifierName(iterator).Args1());
            return Tuple.Create<ExpressionSyntax, Func<IdentifierNameSyntax, ExpressionSyntax>>(
                collection.Accessing("Select").Invoking(iterator.Lambdad(wrappedRead).Args1()),
                e => e.Accessing("Item1"));
        }

        public IEnumerable<CodeIssue> GetIssues(IDocument document, CommonSyntaxToken token, CancellationToken cancellationToken) {
            throw new NotImplementedException();
        }
        public IEnumerable<CodeIssue> GetIssues(IDocument document, CommonSyntaxTrivia trivia, CancellationToken cancellationToken) {
            throw new NotImplementedException();
        }
    }
}
