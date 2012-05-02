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

            var trueBranch = Syntax.Block(statements: Syntax.List(f.Statement.Statements().AppendUnlessJumps(body.Skip(1))));
            var falseBranch = Syntax.Block(statements: Syntax.List((f.ElseOpt == null ? Syntax.Block() : f.ElseOpt.Statement).Statements().AppendUnlessJumps(body.Skip(1))));

            var falseIsEmpty = falseBranch.HasSideEffects(model) <= Analysis.Result.FalseIfCodeFollowsConventions;
            var trueIsEmpty = trueBranch.HasSideEffects(model) <= Analysis.Result.FalseIfCodeFollowsConventions;
            if (falseIsEmpty == trueIsEmpty) return null;

            var condition = f.Condition;
            var action = trueIsEmpty ? falseBranch : trueBranch;

            var reads = action.DescendentNodes()
                        .OfType<IdentifierNameSyntax>()
                        .Where(e => e.Identifier.ValueText == forLoop.Identifier.ValueText)
                        .ToArray();
            var idem = action.IsLoopVarFirstpotent(reads, model);
            var fidem = action.IsLoopVarLastpotent(reads, model);
            if (action.Statements.Last().IsIntraLoopJump()) {
                action = action.With(statements: Syntax.List(action.Statements.SkipLast(1)));
            }
            if (action.HasTopLevelIntraLoopJumps()) return null;
            reads = action.DescendentNodes()
                    .OfType<IdentifierNameSyntax>()
                    .Where(e => e.Identifier.ValueText == forLoop.Identifier.ValueText)
                    .ToArray();
            var chooseFirstOverLast = (int)idem >= (int)fidem;
            
            StatementSyntax equivalentLinqQuery;
            if (reads.Length == 0) {
                if (idem < Analysis.Result.TrueIfCodeFollowsConventions) return null;
                equivalentLinqQuery = Syntax.IfStatement(
                    condition: Syntax.InvocationExpression(
                        expression: Syntax.MemberAccessExpression(SyntaxKind.MemberAccessExpression,
                            expression: forLoop.Expression,
                            name: Syntax.IdentifierName("Any")),
                        argumentList: Syntax.ArgumentList(
                            arguments: Syntax.SeparatedList(Syntax.Argument(
                                expression: Syntax.SimpleLambdaExpression(
                                    parameter: Syntax.Parameter(identifier: forLoop.Identifier),
                                    body: condition))))),
                    statement: action);
                
                var r = new ReadyCodeAction(
                    "for(x){if(y){z}} -> if(x.Any(y)){z}",
                    editFactory,
                    document,
                    forLoop,
                    () => equivalentLinqQuery);
                return new[] { 
                    new CodeIssue(CodeIssue.Severity.Warning, forLoop.ForEachKeyword.Span, "Loop simplifies to If.", new[] { r })
                };
            } else {
                if (Enumerable.Max(new[] { idem, fidem }) < Analysis.Result.TrueIfCodeFollowsConventions) return null;

                var loopVar = model.AnalyzeRegionDataFlow(forLoop.Span).ReadInside.Single(e => e.Name == forLoop.Identifier.ValueText);
                var loopVarType = ((LocalSymbol)loopVar).Type;

                ExpressionSyntax wrappedQuery;
                Func<IdentifierNameSyntax, ExpressionSyntax> unwrapper;
                var localVarName = Syntax.Identifier("_" + forLoop.Identifier.ValueText);
                var localVarAccess = Syntax.IdentifierName(Syntax.Identifier("_" + forLoop.Identifier.ValueText));
                if (loopVarType.IsReferenceType || loopVarType.SpecialType == SpecialType.System_Nullable_T) {
                    var wrappedRead = Syntax.InvocationExpression(
                        Syntax.MemberAccessExpression(SyntaxKind.MemberAccessExpression, 
                            Syntax.IdentifierName("Tuple"), 
                            name: Syntax.IdentifierName("Create")),
                        Syntax.ArgumentList(arguments: Syntax.SeparatedList(Syntax.Argument(expression: reads.First()))));
                    wrappedQuery = Syntax.InvocationExpression(
                        expression: Syntax.MemberAccessExpression(SyntaxKind.MemberAccessExpression,
                            expression: forLoop.Expression,
                            name: Syntax.IdentifierName("Select")),
                        argumentList: Syntax.ArgumentList(
                            arguments: Syntax.SeparatedList(Syntax.Argument(
                                expression: Syntax.SimpleLambdaExpression(
                                    parameter: Syntax.Parameter(identifier: forLoop.Identifier),
                                    body: wrappedRead)))));
                    unwrapper = e => Syntax.MemberAccessExpression(SyntaxKind.MemberAccessExpression,
                        expression: localVarAccess,
                        name: Syntax.IdentifierName("Item1"));
                } else {
                    wrappedQuery = Syntax.InvocationExpression(
                        expression: Syntax.MemberAccessExpression(SyntaxKind.MemberAccessExpression,
                            expression: forLoop.Expression,
                            name: Syntax.GenericName(
                                Syntax.Identifier("Cast"),
                                Syntax.TypeArgumentList(arguments: Syntax.SeparatedList<TypeSyntax>(Syntax.NullableType(Syntax.IdentifierName(loopVarType.Name)))))),
                        argumentList: Syntax.ArgumentList());
                    unwrapper = e => Syntax.MemberAccessExpression(SyntaxKind.MemberAccessExpression,
                        expression: localVarAccess,
                        name: Syntax.IdentifierName("Value"));
                }
                var query = Syntax.InvocationExpression(
                    expression: Syntax.MemberAccessExpression(SyntaxKind.MemberAccessExpression,
                        expression: wrappedQuery,
                        name: Syntax.IdentifierName(chooseFirstOverLast ? "FirstOrDefault" : "LastOrDefault")),
                    argumentList: Syntax.ArgumentList(
                        arguments: Syntax.SeparatedList(Syntax.Argument(
                            expression: Syntax.SimpleLambdaExpression(
                                parameter: Syntax.Parameter(identifier: forLoop.Identifier),
                                body: condition)))));
                var tryAssignIfDo = Syntax.Block(statements: Syntax.List(new StatementSyntax[] {
                    Syntax.LocalDeclarationStatement(declaration: Syntax.VariableDeclaration(
                        Syntax.IdentifierName(Syntax.Token(SyntaxKind.VarKeyword)),
                        Syntax.SeparatedList(Syntax.VariableDeclarator(
                            localVarName,
                            initializerOpt: Syntax.EqualsValueClause(
                                value: query))))),
                    Syntax.IfStatement(
                        condition: Syntax.BinaryExpression(SyntaxKind.NotEqualsExpression, localVarAccess, Syntax.Token(SyntaxKind.ExclamationEqualsToken), Syntax.LiteralExpression(SyntaxKind.NullLiteralExpression)),
                        statement: action.ReplaceNodes(reads, (e,a) => unwrapper(a)))}));
                var r = new ReadyCodeAction(
                    "for(x){if(y){z}} -> {single?(x?);if(y){z}}",
                    editFactory,
                    document,
                    forLoop,
                    () => tryAssignIfDo);
                return new[] { 
                    new CodeIssue(CodeIssue.Severity.Warning, forLoop.ForEachKeyword.Span, "Loop simplifies to If.", new[] { r })
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
