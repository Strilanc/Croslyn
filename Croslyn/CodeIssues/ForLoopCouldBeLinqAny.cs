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
    internal class ForLoopCouldBeLinqAny : ICodeIssueProvider {
        private readonly ICodeActionEditFactory editFactory;

        [ImportingConstructor]
        internal ForLoopCouldBeLinqAny(ICodeActionEditFactory editFactory) {
            this.editFactory = editFactory;
        }

        public IEnumerable<CodeIssue> GetIssues(IDocument document, CommonSyntaxNode node, CancellationToken cancellationToken) {
            var model = document.TryGetSemanticModel();
            if (model == null) return null;

            var forLoop = (ForEachStatementSyntax)node;

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
            var loopBranch = trueIsEmpty ? falseBranch : trueBranch;
            var branchActions = loopBranch.Statements.Last().IsIntraLoopJump() ? loopBranch.Statements.SkipLast(1) : loopBranch.Statements;
            if (branchActions.Any(e => e.HasTopLevelIntraLoopJumps())) return null;

            var reads = loopBranch.DescendentNodes()
                        .OfType<IdentifierNameSyntax>()
                        .Where(e => e.Identifier.ValueText == forLoop.Identifier.ValueText)
                        .ToArray();
            var idem = loopBranch.IsLoopVarFirstpotent(reads, model);
            var fidem = loopBranch.IsLoopVarLastpotent(reads, model);
            var chooseFirstOverLast = (int)idem >= (int)fidem;
            
            StatementSyntax equivalentLinqQuery;
            if (reads.Length == 0) {
                if (idem < Analysis.Result.TrueIfCodeFollowsConventions) return null;
                equivalentLinqQuery = Syntax.IfStatement(
                    condition: forLoop.Expression.Accessing("Any").Invoking(forLoop.Identifier.Lambdad(condition).Args1()),
                    statement: branchActions.Block());
                
                var r = new ReadyCodeAction(
                    "for(x){if(y){z}} -> if(x.Any(y)){z}",
                    editFactory,
                    document,
                    forLoop,
                    () => equivalentLinqQuery);
                return new[] { 
                    new CodeIssue(CodeIssue.Severity.Warning, forLoop.ForEachKeyword.Span, "Loop can be simplified by using a Linq query.", new[] { r })
                };
            } else {
                if (Enumerable.Max(new[] { idem, fidem }) < Analysis.Result.TrueIfCodeFollowsConventions) return null;

                var loopVar = model.AnalyzeRegionDataFlow(forLoop.Span).ReadInside.Single(e => e.Name == forLoop.Identifier.ValueText);
                var loopVarType = ((LocalSymbol)loopVar).Type;

                ExpressionSyntax nullableQuery;
                Func<IdentifierNameSyntax, ExpressionSyntax> unullGetter;
                if (loopVarType.IsReferenceType || loopVarType.SpecialType == SpecialType.System_Nullable_T) {
                    var wrappedRead = Syntax.IdentifierName("Tuple").Accessing("Create").Invoking(reads.First().Args1());
                    nullableQuery = forLoop.Expression.Accessing("Select").Invoking(forLoop.Identifier.Lambdad(wrappedRead).Args1());
                    unullGetter = e => e.Accessing("Item1");
                } else {
                    nullableQuery = forLoop.Expression.Accessing(Syntax.Identifier("Cast").Genericed(loopVarType.Name.AsIdentifier().Nullable())).Invoking();
                    unullGetter = e => e.Accessing("Value");
                }
                var condReads = condition.DescendentNodes()
                                .OfType<IdentifierNameSyntax>()
                                .Where(e => e.Identifier.ValueText == forLoop.Identifier.ValueText);
                var unwrappedCond = condition.ReplaceNodes(condReads, (e, a) => unullGetter(a));

                var queryTypeDesc = chooseFirstOverLast ? "First?" : "Last?";
                var query = nullableQuery.Accessing(chooseFirstOverLast ? "FirstOrDefault" : "LastOrDefault")
                                         .Invoking(forLoop.Identifier.Lambdad(unwrappedCond).Args1());
                
                var tempNullableLocalName = Syntax.Identifier("_" + forLoop.Identifier.ValueText);
                var tempNullableLocalGet = Syntax.IdentifierName(tempNullableLocalName);
                var queryCheckDo = new StatementSyntax[] {
                    tempNullableLocalName.varInit(query),
                    Syntax.IfStatement(
                        condition: Syntax.BinaryExpression(SyntaxKind.NotEqualsExpression, tempNullableLocalGet, Syntax.Token(SyntaxKind.ExclamationEqualsToken), Syntax.LiteralExpression(SyntaxKind.NullLiteralExpression)),
                        statement: branchActions.Prepend(forLoop.Identifier.varInit(unullGetter(tempNullableLocalGet))).Block())};
                var r = forLoop.MakeReplaceStatementWithManyAction(queryCheckDo, "for(x){if(y){z}} -> if(x." + queryTypeDesc + "(y)){z}", editFactory, document);
                return new[] { 
                    new CodeIssue(CodeIssue.Severity.Warning, forLoop.ForEachKeyword.Span, "Loop can be simplified by using a Linq query and a temporary local.", new[] { r })
                };
            }
        }

        public IEnumerable<CodeIssue> GetIssues(IDocument document, CommonSyntaxToken token, CancellationToken cancellationToken) {
            throw new NotImplementedException();
        }
        public IEnumerable<CodeIssue> GetIssues(IDocument document, CommonSyntaxTrivia trivia, CancellationToken cancellationToken) {
            throw new NotImplementedException();
        }
    }
}
