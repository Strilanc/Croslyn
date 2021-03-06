﻿using System;
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
    public class ReducibleBooleanExpression : ICodeIssueProvider {
        public IEnumerable<CodeIssue> GetIssues(IDocument document, CommonSyntaxNode node, CancellationToken cancellationToken) {
            var model = document.GetSemanticModel();
            var b = (BinaryExpressionSyntax)node;
            var actions = GetSimplifications(b, model, Assumptions.All, cancellationToken)
                          .Select(e => e.AsCodeAction(document))
                          .ToArray();
            if (actions.Length == 0) yield break;

            yield return new CodeIssue(
                CodeIssue.Severity.Warning,
                b.OperatorToken.Span,
                "Boolean operation can be simplified.",
                actions);
        }

        public static IEnumerable<ReplaceAction> GetSimplifications(BinaryExpressionSyntax b, ISemanticModel model, Assumptions assume, CancellationToken cancellationToken = default(CancellationToken)) {
            if (!b.Left.DefinitelyHasBooleanType(model)) yield break;
            if (!b.Right.DefinitelyHasBooleanType(model)) yield break;

            // prep basic analysis
            var leftEffects = b.Left.HasSideEffects(model, assume) != false;
            var rightEffects = b.Right.HasSideEffects(model, assume) != false;
            var lv = b.Left.TryGetConstBoolValue();
            var rv = b.Right.TryGetConstBoolValue();
            var cmp = b.Left.TryEvalAlternativeComparison(b.Right, model, assume);

            // prep utility funcs for adding simplifications
            var actions = new List<ReplaceAction>();
            Action<String, ExpressionSyntax> include = (desc, rep) => 
                actions.Add(new ReplaceAction(desc, b, rep));
            Action<bool> useRight = v => { 
                if (!leftEffects) 
                    include(v ? "rhs" : "!rhs", b.Right.MaybeInverted(!v)); 
            };
            Action<bool> useLeft = v => { 
                if (!rightEffects) 
                    include(v ? "lhs" : "!lhs", b.Left.MaybeInverted(!v)); 
            };
            Action<bool> useBool = v => {
                if (!leftEffects && !rightEffects) {
                    actions.Clear(); // override left/right
                    include(v + "", v.AsLiteral());
                }
            };

            // try to simplify equality operators ==, !=, ^
            bool? equality = null;
            if (b.Kind == SyntaxKind.EqualsExpression) equality = true;
            if (b.Kind == SyntaxKind.ExclusiveOrExpression || b.Kind == SyntaxKind.NotEqualsExpression) equality = false;
            if (equality != null) {
                if (lv != null) useRight(lv == equality);
                if (rv != null) useLeft(rv == equality);
                if (cmp != null) useBool(cmp == equality);
            }
            
            // try to simplify and/or operators &&, &, ||, |
            var sticky = b.Kind.IsAndBL() ? false 
                       : b.Kind.IsOrBL() ? true
                       : (bool?)null;
            if (sticky != null) {
                if (b.Kind.IsShortCircuitingLogic() && lv == sticky) rightEffects = false; // short-circuit prevents effects
                if (cmp == true || lv == !sticky) useRight(true);
                if (cmp == true || rv == !sticky) useLeft(true);
                if (cmp == false || lv == sticky || rv == sticky) useBool(sticky.Value);
            }

            // expose simplifications as code issues/actions
            foreach (var action in actions)
                yield return action;
        }

        public IEnumerable<CodeIssue> GetIssues(IDocument document, CommonSyntaxToken token, CancellationToken cancellationToken) {
            throw new NotImplementedException();
        }
        public IEnumerable<CodeIssue> GetIssues(IDocument document, CommonSyntaxTrivia trivia, CancellationToken cancellationToken) {
            throw new NotImplementedException();
        }
    }
}
