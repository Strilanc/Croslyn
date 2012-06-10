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
    public class ForEachToFirstLast : ICodeIssueProvider {
        public IEnumerable<CodeIssue> GetIssues(IDocument document, CommonSyntaxNode node, CancellationToken cancellationToken) {
            var forLoop = (ForEachStatementSyntax)node;
            var model = document.GetSemanticModel();
            var simplifications = GetSimplifications(forLoop, model, Assumptions.All, cancellationToken);
            return simplifications.Select(e => new CodeIssue(
                CodeIssue.Severity.Warning,
                forLoop.ForEachKeyword.Span,
                "Single execution of 'for each' loop body is sufficient.",
                new[] { e.AsCodeAction(document) }));
        }
        public static IEnumerable<ReplaceAction> GetSimplifications(ForEachStatementSyntax forLoop, ISemanticModel model, Assumptions assumptions, CancellationToken cancellationToken = default(CancellationToken)) {
            if (forLoop.IsAnyIterationSufficient(model, assumptions) == true) 
                yield break; // a more appropriate code issue handles this case

            // can the loop be replaced by its first or last iteration?
            var isFirstSufficient = forLoop.IsFirstIterationSufficient(model, assumptions) == true;
            var isLastSufficient = forLoop.IsLastIterationSufficient(model, assumptions) == true;
            var firstVsLast = isFirstSufficient ? "First"
                            : isLastSufficient ? "Last"
                            : null;
            if (firstVsLast == null) yield break;

            // do we know how to translate?
            var loopStatements = forLoop.Statement.Statements();
            var rawThenStatements = loopStatements.SkipLast(loopStatements.Last().IsIntraLoopJump() ? 1 : 0).ToArray();
            if (rawThenStatements.Any(e => e.HasTopLevelIntraLoopJumps())) yield break; // don't know how to translate jumps that aren't at the end

            // wrap collection items in a type with a new null value (so that a null result definitively indicates an empty collection)
            var iteratorType = ((LocalSymbol)model.GetDeclaredSymbol(forLoop)).Type;
            var nuller = GetNullabledQueryAndValueGetter(iteratorType, forLoop.Identifier, forLoop.Expression);
            var nullableQuery = nuller.Item1;
            var valueGetter = nuller.Item2;
            var tempNullableLocalName = Syntax.Identifier("_" + forLoop.Identifier.ValueText);
            var tempNullableLocalGet = Syntax.IdentifierName(tempNullableLocalName);

            // build replacement
            var iteratorReads = forLoop.Statement.ReadsOfLocalVariable(forLoop.Identifier).ToArray();
            var desiredIterationQuery = nullableQuery.Accessing(firstVsLast + "OrDefault").Invoking();
            var condition = tempNullableLocalGet.BOpNotEquals(Syntax.LiteralExpression(SyntaxKind.NullLiteralExpression));
            var useDenulledLocal = iteratorReads.Length > 2;
            var thenStatement = useDenulledLocal
                              ? rawThenStatements.Prepend(forLoop.Identifier.VarInit(valueGetter(tempNullableLocalGet))).Block()
                              : rawThenStatements.Select(e => e.ReplaceNodes(iteratorReads, (n, a) => valueGetter(tempNullableLocalGet))).Block();
            var replacementStatements = new StatementSyntax[] {
                tempNullableLocalName.VarInit(desiredIterationQuery),
                condition.IfThen(thenStatement)
            };

            // expose as code action/issue
            yield return forLoop.MakeReplaceStatementWithManyAction(
                replacementStatements,
                "Execute " + firstVsLast + " if any");
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
