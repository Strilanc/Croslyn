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
    [ExportSyntaxNodeCodeIssueProvider("Croslyn", LanguageNames.CSharp, typeof(BinaryExpressionSyntax))]
    internal class RedundantBooleanExpression : ICodeIssueProvider {
        private readonly ICodeActionEditFactory editFactory;

        [ImportingConstructor]
        internal RedundantBooleanExpression(ICodeActionEditFactory editFactory) {
            this.editFactory = editFactory;
        }

        public IEnumerable<CodeIssue> GetIssues(IDocument document, CommonSyntaxNode node, CancellationToken cancellationToken) {
            var model = document.TryGetSemanticModel();
            if (model == null) return null;

            var binaryNode = (BinaryExpressionSyntax)node;
            if (!binaryNode.Left.DefinitelyHasBooleanType(model)) return null;
            if (!binaryNode.Right.DefinitelyHasBooleanType(model)) return null;

            var leftEffects = binaryNode.Left.HasSideEffects(model) > Analysis.Result.FalseIfCodeFollowsConventions;
            var rightEffects = binaryNode.Right.HasSideEffects(model) > Analysis.Result.FalseIfCodeFollowsConventions;
            var shortCircuits = binaryNode.Kind == SyntaxKind.LogicalOrExpression || binaryNode.Kind == SyntaxKind.LogicalAndExpression;

            var lv = binaryNode.Left.TryGetConstBoolValue();
            var rv = binaryNode.Right.TryGetConstBoolValue();
            var cmp = binaryNode.Left.TryLocalBoolCompare(binaryNode.Right, model);

            var useFalse = new ReadyCodeAction("false", editFactory, document, binaryNode, () => Syntax.LiteralExpression(SyntaxKind.FalseLiteralExpression));
            var useTrue = new ReadyCodeAction("true", editFactory, document, binaryNode, () => Syntax.LiteralExpression(SyntaxKind.TrueLiteralExpression));
            var useRight = new ReadyCodeAction("rhs", editFactory, document, binaryNode, () => binaryNode.Right);
            var useInvertedRight = new ReadyCodeAction("!rhs", editFactory, document, binaryNode, () => binaryNode.Right.Inverted());
            var useLeft = new ReadyCodeAction("lhs", editFactory, document, binaryNode, () => binaryNode.Left);
            var useInvertedLeft = new ReadyCodeAction("!lhs", editFactory, document, binaryNode, () => binaryNode.Left.Inverted());
            
            var actions = new List<ICodeAction>();
            if (binaryNode.Kind == SyntaxKind.EqualsExpression) {
                if (lv != null) actions.Add(lv.Value ? useRight : useInvertedRight);
                if (rv != null) actions.Add(rv.Value ? useLeft : useInvertedLeft);
                if (cmp != null && !leftEffects && !rightEffects) actions.Add(cmp.Value ? useTrue : useFalse);
            } else if (binaryNode.Kind == SyntaxKind.LogicalAndExpression || binaryNode.Kind == SyntaxKind.BitwiseAndExpression) {
                if (lv == false && (shortCircuits || !rightEffects)) actions.Add(useFalse);
                if (lv == true) actions.Add(useRight);
                if (rv == true) actions.Add(useLeft);
                if (rv == false && !leftEffects) actions.Add(useFalse);
                if (cmp == false && !rightEffects && !leftEffects) actions.Add(useFalse);
                if (cmp == true && !rightEffects) actions.Add(useLeft);
                if (cmp == true && !leftEffects) actions.Add(useRight);
            } else if (binaryNode.Kind == SyntaxKind.LogicalOrExpression || binaryNode.Kind == SyntaxKind.BitwiseOrExpression) {
                if (lv == true && (shortCircuits || !rightEffects)) actions.Add(useTrue);
                if (lv == false) actions.Add(useRight);
                if (rv == false) actions.Add(useLeft);
                if (rv == true && !leftEffects) actions.Add(useTrue);
                if (cmp == false && !rightEffects && !leftEffects) actions.Add(useTrue);
                if (cmp == true && !rightEffects) actions.Add(useLeft);
                if (cmp == true && !leftEffects) actions.Add(useRight);
            } else if (binaryNode.Kind == SyntaxKind.ExclusiveOrExpression || binaryNode.Kind == SyntaxKind.NotEqualsExpression) {
                if (lv != null) actions.Add(lv.Value ? useInvertedLeft : useLeft);
                if (rv != null) actions.Add(rv.Value ? useInvertedRight : useRight);
                if (cmp != null && !leftEffects && !rightEffects) actions.Add(cmp.Value ? useFalse : useTrue);
            }
            if (actions.Count == 0) return null;
            return new[] { new CodeIssue(CodeIssue.Severity.Warning, binaryNode.OperatorToken.Span, "Reducible boolean operation", actions.Distinct().ToArray()) };
        }

        public IEnumerable<CodeIssue> GetIssues(IDocument document, CommonSyntaxToken token, CancellationToken cancellationToken) {
            throw new NotImplementedException();
        }
        public IEnumerable<CodeIssue> GetIssues(IDocument document, CommonSyntaxTrivia trivia, CancellationToken cancellationToken) {
            throw new NotImplementedException();
        }
    }
}
