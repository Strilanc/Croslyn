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
    internal class ForEachToIf : ICodeIssueProvider {
        private readonly ICodeActionEditFactory editFactory;

        [ImportingConstructor]
        internal ForEachToIf(ICodeActionEditFactory editFactory) {
            this.editFactory = editFactory;
        }

        public IEnumerable<CodeIssue> GetIssues(IDocument document, CommonSyntaxNode node, CancellationToken cancellationToken) {
            var forLoop = (ForEachStatementSyntax)node;
            var model = document.TryGetSemanticModel();
            if (model == null) return null;

            var body = forLoop.Statement.Statements();
            if (body.None()) return null;
            var f = body.First() as IfStatementSyntax;
            if (f == null) return null;

            var trueBranch = f.Statement.Statements().AppendUnlessJumps(body.Skip(1)).Block();
            var falseBranch = (f.ElseOpt == null ? Syntax.Block() : f.ElseOpt.Statement).Statements().AppendUnlessJumps(body.Skip(1)).Block();

            var falseIsEmpty = falseBranch.HasSideEffects(model) <= Analysis.Result.FalseIfCodeFollowsConventions;
            var trueIsEmpty = trueBranch.HasSideEffects(model) <= Analysis.Result.FalseIfCodeFollowsConventions;
            if (falseIsEmpty == trueIsEmpty) return null;

            var condition = f.Condition;
            var originalConditionalBranch = trueIsEmpty ? falseBranch : trueBranch;
            var conditionalActions = originalConditionalBranch.Statements.Last().IsIntraLoopJump() 
                                   ? originalConditionalBranch.Statements.SkipLast(1) 
                                   : originalConditionalBranch.Statements;
            if (conditionalActions.Any(e => e.HasTopLevelIntraLoopJumps())) return null;

            var iterReads = originalConditionalBranch.ReadsOfLocalVariable(forLoop.Identifier).ToArray();
            var isFirstMatchDefinitive = originalConditionalBranch.IsLoopVarFirstpotent(iterReads, model);
            
            if (iterReads.Length == 0) {
                // use simpler 'if any' transformation when possible
                if (isFirstMatchDefinitive < Analysis.Result.TrueIfCodeFollowsConventions) return null;
                return AnyTransform(forLoop, condition, conditionalActions, document);
            }

            var isLastMatchDefinitive = originalConditionalBranch.IsLoopVarLastpotent(iterReads, model);
            if (Enumerable.Max(new[] { isFirstMatchDefinitive, isLastMatchDefinitive }) < Analysis.Result.TrueIfCodeFollowsConventions) return null;
            var firstVsLast = (int)isFirstMatchDefinitive >= (int)isLastMatchDefinitive ? "First" : "Last";

            var iterator = model.AnalyzeRegionDataFlow(forLoop.Span).ReadInside.Single(e => e.Name == forLoop.Identifier.ValueText);
            var iteratorType = ((LocalSymbol)iterator).Type;

            return FirstOrLastTransform(forLoop, condition, conditionalActions, document, firstVsLast, iteratorType);
        }
        private CodeIssue[] AnyTransform(ForEachStatementSyntax forLoop, 
                                         ExpressionSyntax condition, 
                                         IEnumerable<StatementSyntax> conditionalActions,
                                         IDocument document) {
            var ifAnyThenStatement = Syntax.IfStatement(
                condition: forLoop.Expression.Accessing("Any").Invoking(forLoop.Identifier.Lambdad(condition).Args1()),
                statement: conditionalActions.Block());

            var toIfAnyThenAction = new ReadyCodeAction(
                "for(x){if(y){z}} -> if(x.Any(y)){z}",
                editFactory,
                document,
                forLoop,
                () => ifAnyThenStatement);

            return toIfAnyThenAction.CodeIssues1(
                CodeIssue.Severity.Warning,
                forLoop.ForEachKeyword.Span,
                "Loop can be simplified by using a Linq query.");
        }
        private CodeIssue[] FirstOrLastTransform(ForEachStatementSyntax forLoop,
                                                 ExpressionSyntax condition,
                                                 IEnumerable<StatementSyntax> conditionalActions,
                                                 IDocument document,
                                                 string firstVsLast,
                                                 TypeSymbol iteratorType) {
            
            var nuller = GetNullabledQueryAndValueGetter(iteratorType, forLoop.Identifier, forLoop.Expression);
            var nullableQuery = nuller.Item1;
            var valueGetter = nuller.Item2;
            
            var query = nullableQuery
                        .Accessing(firstVsLast + "OrDefault")
                        .Invoking(forLoop.Identifier.Lambdad(
                            condition.ReplaceNodes(
                                condition.ReadsOfLocalVariable(forLoop.Identifier),
                                (e, a) => valueGetter(a))).Args1());

            var tempNullableLocalName = Syntax.Identifier("_" + forLoop.Identifier.ValueText);
            var tempNullableLocalGet = Syntax.IdentifierName(tempNullableLocalName);

            var ifFirstOrLastThenStatements = new StatementSyntax[] {
                tempNullableLocalName.VarInit(query),
                tempNullableLocalGet.BOpNotEquals(Syntax.LiteralExpression(SyntaxKind.NullLiteralExpression))
                    .IfThen(conditionalActions.Prepend(forLoop.Identifier.VarInit(valueGetter(tempNullableLocalGet))).Block())};

            var switchToFirstLastTemp = forLoop.MakeReplaceStatementWithManyAction(
                ifFirstOrLastThenStatements,
                "for(x){if(y){z}} -> if(x." + firstVsLast + "?(y)){z}",
                editFactory,
                document);

            return switchToFirstLastTemp.CodeIssues1(
                CodeIssue.Severity.Warning,
                forLoop.ForEachKeyword.Span,
                "Loop can be simplified by using a Linq query and a temporary local.");
        }
        private static Tuple<ExpressionSyntax, Func<IdentifierNameSyntax, ExpressionSyntax>> GetNullabledQueryAndValueGetter(TypeSymbol itemType, SyntaxToken iterator, ExpressionSyntax collection) {
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
