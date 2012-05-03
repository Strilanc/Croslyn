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
    internal class ForEachToFirst : ICodeIssueProvider {
        private readonly ICodeActionEditFactory editFactory;

        [ImportingConstructor]
        internal ForEachToFirst(ICodeActionEditFactory editFactory) {
            this.editFactory = editFactory;
        }

        public IEnumerable<CodeIssue> GetIssues(IDocument document, CommonSyntaxNode node, CancellationToken cancellationToken) {
            var forLoop = (ForEachStatementSyntax)node;
            var model = document.TryGetSemanticModel();
            if (model == null) return null;

            var body = forLoop.Statement.Statements();
            if (body.None()) return null;

            var bodyWithoutJump = body.SkipLast(body.Last().IsIntraLoopJump() ? 1 : 0);
            if (bodyWithoutJump.Any(e => e.HasTopLevelIntraLoopJumps())) return null;

            var iterReads = forLoop.Statement.ReadsOfLocalVariable(forLoop.Identifier).ToArray();
            if (forLoop.Statement.IsLoopVarFirstpotent(iterReads, model) < Analysis.Result.TrueIfCodeFollowsConventions) return null;
            if (iterReads.Length == 0) return null;

            var iterator = model.AnalyzeRegionDataFlow(forLoop.Statement.Span).ReadInside.First(e => e.Name == forLoop.Identifier.ValueText);
            var iteratorType = ((LocalSymbol)iterator).Type;

            var nuller = GetNullabledQueryAndValueGetter(iteratorType, forLoop.Identifier, forLoop.Expression);
            var nullableQuery = nuller.Item1;
            var valueGetter = nuller.Item2;

            var query = nullableQuery.Accessing("FirstOrDefault").Invoking();

            var tempNullableLocalName = Syntax.Identifier("_" + forLoop.Identifier.ValueText);
            var tempNullableLocalGet = Syntax.IdentifierName(tempNullableLocalName);

            var ifFirstStatements = new StatementSyntax[] {
                tempNullableLocalName.VarInit(query),
                tempNullableLocalGet.BOpNotEquals(Syntax.LiteralExpression(SyntaxKind.NullLiteralExpression))
                    .IfThen(bodyWithoutJump.Prepend(forLoop.Identifier.VarInit(valueGetter(tempNullableLocalGet))).Block())};

            var switchToIfFirstStatements = forLoop.MakeReplaceStatementWithManyAction(
                ifFirstStatements,
                "for(c){a} -> if(c.First?()){a}",
                editFactory,
                document);

            return switchToIfFirstStatements.CodeIssues1(
                CodeIssue.Severity.Warning,
                forLoop.ForEachKeyword.Span,
                "Loop can be simplified by using a Linq query and a temporary local.");
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
