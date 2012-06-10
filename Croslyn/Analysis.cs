using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Roslyn.Compilers.CSharp;
using System.Diagnostics.Contracts;
using Roslyn.Services;
using Roslyn.Compilers.Common;
using Strilbrary.Collections;
using Roslyn.Compilers;

public static class Analysis {
    public static readonly IEnumerable<SyntaxKind> ProbablySafeBinaryOperators = new[] {
                SyntaxKind.EqualsExpression,
                SyntaxKind.NotEqualsExpression,
                SyntaxKind.AddExpression,
                SyntaxKind.PlusExpression,
                SyntaxKind.SubtractExpression,
                SyntaxKind.MultiplyExpression,
                SyntaxKind.DivideExpression,
                SyntaxKind.ModuloExpression,
                SyntaxKind.LessThanExpression,
                SyntaxKind.LessThanOrEqualExpression,
                SyntaxKind.GreaterThanExpression,
                SyntaxKind.GreaterThanOrEqualExpression,
                SyntaxKind.BitwiseAndExpression,
                SyntaxKind.BitwiseOrExpression,
                SyntaxKind.ExclusiveOrExpression,
                SyntaxKind.LogicalAndExpression,
                SyntaxKind.LogicalOrExpression,
                SyntaxKind.CoalesceExpression,
                SyntaxKind.LeftShiftExpression,
                SyntaxKind.RightShiftExpression,
            };
    public static readonly IEnumerable<SyntaxKind> AssignmentOperatorKinds = new[] {
                SyntaxKind.AddAssignExpression,
                SyntaxKind.AndAssignExpression,
                SyntaxKind.AssignExpression,
                SyntaxKind.DivideAssignExpression,
                SyntaxKind.ExclusiveOrAssignExpression,
                SyntaxKind.LeftShiftAssignExpression,
                SyntaxKind.ModuloAssignExpression,
                SyntaxKind.MultiplyAssignExpression,
                SyntaxKind.OrAssignExpression,
                SyntaxKind.RightShiftAssignExpression,
                SyntaxKind.SubtractAssignExpression,
            };

    public static bool IsShortCircuitingBinaryExpression(this SyntaxKind kind) {
        return kind == SyntaxKind.LogicalAndExpression || kind == SyntaxKind.LogicalOrExpression || kind == SyntaxKind.CoalesceExpression;
    }
    public static bool IsShortCircuitingLogic(this SyntaxKind kind) {
        return kind == SyntaxKind.LogicalAndExpression || kind == SyntaxKind.LogicalOrExpression;
    }
    public static bool IsOrBL(this SyntaxKind kind) {
        return kind == SyntaxKind.LogicalOrExpression || kind == SyntaxKind.BitwiseOrExpression;
    }
    public static bool IsAndBL(this SyntaxKind kind) {
        return kind == SyntaxKind.LogicalAndExpression || kind == SyntaxKind.BitwiseAndExpression;
    }
    ///<summary>The statements in a block statement or, if not a block statement, the single statement.</summary>
    public static StatementSyntax[] Statements(this StatementSyntax e) {
        return e is BlockSyntax ? ((BlockSyntax)e).Statements.ToArray() : new[] { e };
    }
    public static StatementSyntax[] CollapsedStatements(this StatementSyntax e) {
        return e is BlockSyntax 
             ? ((BlockSyntax)e).Statements.SelectMany(f => f.CollapsedStatements()).ToArray() 
             : new[] { e };
    }

    public static IEnumerable<IdentifierNameSyntax> ReadsOfLocalVariable(this SyntaxNode scope, SyntaxToken localVar) {
        return scope.DescendantNodes()
               .OfType<IdentifierNameSyntax>()
               .Where(e => e.Identifier.ValueText == localVar.ValueText);
    }

    public static bool? TryEvalAlternativeComparison(this ExpressionSyntax expression, ExpressionSyntax other, ISemanticModel model) {
        var val1 = expression.TryGetConstBoolValue();
        var val2 = other.TryGetConstBoolValue();
        if (val1.HasValue != val2.HasValue) return null;
        if (val1.HasValue) return val1.Value == val2.Value;

        if (expression is ParenthesizedExpressionSyntax) return ((ParenthesizedExpressionSyntax)expression).Expression.TryEvalAlternativeComparison(other, model);
        if (other is ParenthesizedExpressionSyntax) return expression.TryEvalAlternativeComparison(((ParenthesizedExpressionSyntax)other).Expression, model);
        if (expression.Kind == SyntaxKind.LogicalNotExpression) return !((PrefixUnaryExpressionSyntax)expression).Operand.TryEvalAlternativeComparison(other, model);
        if (other.Kind == SyntaxKind.LogicalNotExpression) return !expression.TryEvalAlternativeComparison(((PrefixUnaryExpressionSyntax)other).Operand, model);

        if (expression.HasSideEffects(model).IsProbablyFalse 
            && expression.WithoutAnyTriviaOrInternalTrivia().ToString() == other.WithoutAnyTriviaOrInternalTrivia().ToString()) 
            return true;
        
        return null;
    }
    public static bool? TryGetConstBoolValue(this ExpressionSyntax expression) {
        if (expression.Kind == SyntaxKind.TrueLiteralExpression) return true;
        if (expression.Kind == SyntaxKind.FalseLiteralExpression) return false;
        if (expression is ParenthesizedExpressionSyntax) return ((ParenthesizedExpressionSyntax)expression).Expression.TryGetConstBoolValue();
        if (expression.Kind == SyntaxKind.LogicalNotExpression) return !((PrefixUnaryExpressionSyntax)expression).Operand.TryGetConstBoolValue();
        return null;
    }

    public static String TryGetCodeBlockOrAreaDescription(this SyntaxNode e) {
        if (e is ClassDeclarationSyntax) return "Class";
        if (e is MethodDeclarationSyntax) return ((MethodDeclarationSyntax)e).Body == null ? null : "Method";
        if (e is StructDeclarationSyntax) return "Struct";
        if (e is BlockSyntax) return "Block";
        if (e is IfStatementSyntax) return "If Block";
        if (e is WhileStatementSyntax) return "While Loop";
        if (e is ForStatementSyntax) return "For Loop";
        if (e is NamespaceDeclarationSyntax) return "Namespace";
        return null;
    }

    public static bool? CompleteExecutionGuaranteesChildExecutedExactlyOnce(this SyntaxNode executed, SyntaxNode child) {
        if (child == executed) return true;

        var parent = child.Parent;
        if (parent is BlockSyntax
            || parent is ExpressionStatementSyntax
            || parent is ArgumentSyntax
            || parent is ArgumentListSyntax
            || parent is InvocationExpressionSyntax 
            || parent is MemberAccessExpressionSyntax) {
            return executed.CompleteExecutionGuaranteesChildExecutedExactlyOnce(parent);
        }
        if (parent is IfStatementSyntax) return parent == executed;
        if (parent is WhileStatementSyntax) return null;
        if (parent is ForEachStatementSyntax) return null;
        if (parent is BinaryExpressionSyntax) {
            if (IsShortCircuitingBinaryExpression(parent.Kind) && child != ((BinaryExpressionSyntax)parent).Left) return null;
            return executed.CompleteExecutionGuaranteesChildExecutedExactlyOnce(parent);
        }
        if (parent is ConditionalExpressionSyntax) {
            var c = (ConditionalExpressionSyntax)parent;
            if (child == c.Condition) return executed.CompleteExecutionGuaranteesChildExecutedExactlyOnce(parent);
            return null;
        }
        return null;
    }
    public static bool IsGuaranteedToJumpOut(this StatementSyntax node, bool includeContinue = true) {
        Contract.Requires(node != null);
        if (node is ReturnStatementSyntax) return true;
        if (node is ThrowStatementSyntax) return true;
        if (node is BreakStatementSyntax) return true;
        if (node is ContinueStatementSyntax) return includeContinue;
        var i = node as IfStatementSyntax;
        if (i != null) return i.Else != null && i.Statement.IsGuaranteedToJumpOut(includeContinue) && i.Else.Statement.IsGuaranteedToJumpOut(includeContinue);
        var b = node as BlockSyntax;
        if (b != null) return b.Statements.Count > 0 && b.Statements.Last().IsGuaranteedToJumpOut(includeContinue);
        return false;
    }
    public static bool DefinitelyHasBooleanType(this ExpressionSyntax expression, ISemanticModel model) {
        var type = model.GetTypeInfo(expression).Type;
        if (type == null) return false;
        return type.SpecialType == SpecialType.System_Boolean;
    }
    public static bool? IsAnyIterationSufficient(this ForEachStatementSyntax syntax, ISemanticModel model, Assumptions assume) {
        Contract.Requires(syntax != null);
        Contract.Requires(model != null);
        
        if (!assume.IterationHasNoSideEffects) return null;
        
        // shouldn't depend on iterator value
        if (model.AnalyzeStatementDataFlow(syntax.Statement).ReadInside.Contains(model.GetDeclaredSymbol(syntax)))
            return null; // probably false, but uses might happen to cancel
        // always jumping out of the loop on the first iteration, and independence from iterator value, should mean equivalence
        // unless the collection iterator has side-effects... but that's bad form, so probably true
        if (syntax.IsGuaranteedToJumpOut(includeContinue: false)) 
            return true;
        return syntax.Statement.IsIdempotent(model).ProbableResult;
    }
    public static bool? IsConst(this ExpressionSyntax syntax, ISemanticModel model) {
        if (syntax is LiteralExpressionSyntax) return true;
        if (syntax is DefaultExpressionSyntax) return true;
        if (syntax is ParenthesizedExpressionSyntax) return (syntax as ParenthesizedExpressionSyntax).Expression.IsConst(model);
        if (ProbablySafeBinaryOperators.Contains(syntax.Kind)) {
            var b = (BinaryExpressionSyntax)syntax;
            return b.Left.IsConst(model).Min(b.Right.IsConst(model));
        }
        return null;
    }
    public static TentativeBool IsIdempotent(this StatementSyntax syntax, ISemanticModel model) {
        if (syntax is EmptyStatementSyntax) return true;
        if (syntax.IsGuaranteedToJumpOut()) return true;

        if (syntax is BlockSyntax) {
            var b = (BlockSyntax)syntax;
            if (b.Statements.Count == 0) return true;
            if (b.Statements.Count == 1) return b.Statements.Single().IsIdempotent(model);
            var m = syntax.Statements().Min(e => e.IsIdempotent(model));
            if (!m.IsProbablyTrue) return m;
            var assigned = syntax.DescendantNodes()
                           .Where(e => e.Kind == SyntaxKind.AssignExpression)
                           .Cast<BinaryExpressionSyntax>()
                           .Select(e => e.Left)
                           .ToArray();
            var assignedSymbols = assigned.Select(e => model.GetSymbolInfo(e));
            var reads = syntax.DescendantNodes()
                        .Except(assigned)
                        .Select(e => model.GetSymbolInfo(e));
            if (!reads.Intersect(assignedSymbols).Any()) return TentativeBool.ProbablyTrue;
            return TentativeBool.Unknown;
        }
        if (syntax is ExpressionStatementSyntax) 
            return ((ExpressionStatementSyntax)syntax).Expression.IsIdempotent(model);
        return TentativeBool.Unknown;
    }
    public static TentativeBool IsIdempotent(this ExpressionSyntax syntax, ISemanticModel model) {
        if (syntax is ParenthesizedExpressionSyntax) return ((ParenthesizedExpressionSyntax)syntax).Expression.IsIdempotent(model);
        if (syntax is IdentifierNameSyntax) return true;
        if (syntax is InvocationExpressionSyntax) return TentativeBool.Unknown;
        if (syntax is MemberAccessExpressionSyntax) {
            var m = (MemberAccessExpressionSyntax)syntax;
            return m.Expression.IsIdempotent(model).Min(TentativeBool.ProbablyTrue);
        };
        if (syntax.Kind == SyntaxKind.AssignExpression) {
            var b = (BinaryExpressionSyntax)syntax;
            if (b.Left is IdentifierNameSyntax) {
                var effects = b.Right.HasSideEffects(model);
                if (effects.IsProbablyFalse) return effects.Inverse;
            }
        } else if (AssignmentOperatorKinds.Contains(syntax.Kind)) {
            return TentativeBool.ProbablyFalse;
        }

        var isConst = syntax.IsConst(model);
        if (isConst == true) return true;

        return TentativeBool.Unknown;
    }

    public static bool? IsFirstIterationSufficient(this ForEachStatementSyntax syntax, ISemanticModel model, Assumptions assume) {
        Contract.Requires(syntax != null);
        Contract.Requires(model != null);
        
        if (!assume.IterationHasNoSideEffects) return null;
        
        // always breaks outs? then only the first iteration CAN happen
        if (syntax.Statement.IsGuaranteedToJumpOut(includeContinue: false)) return true;
        
        // any iteration works? then so does the first one
        var anyIsGood = syntax.IsAnyIterationSufficient(model, assume);
        if (anyIsGood == true) return true;

        return null;
    }

    public static bool? IsLastIterationSufficient(this ForEachStatementSyntax syntax, ISemanticModel model, Assumptions assume) {
        Contract.Requires(syntax != null);
        Contract.Requires(model != null);

        if (!assume.IterationHasNoSideEffects) return null;

        // any iteration works? then so does the last one
        var anyIsGood = syntax.IsAnyIterationSufficient(model, assume);
        if (anyIsGood == true) return true;

        if (syntax.DescendantNodes(e => e is StatementSyntax).Any(e => e is BreakStatementSyntax || e is ReturnStatementSyntax || e is ThrowStatementSyntax))
            return null; //loop might end before last iteration

        return syntax.Statement.IsLastIterationSufficient_Helper(model, assume, model.GetDeclaredSymbol(syntax));
    }
    private static bool? IsLastIterationSufficient_Helper(this StatementSyntax syntax, ISemanticModel model, Assumptions assume, ISymbol iteratorVariable) {
        Contract.Requires(syntax != null);
        Contract.Requires(model != null);
        Contract.Requires(iteratorVariable != null);

        if (syntax is ContinueStatementSyntax) return true;
        if (syntax is BlockSyntax) {
            var b = (BlockSyntax)syntax;
            if (b.Statements.Count == 0) return true;
            if (b.Statements.Count == 1) return b.Statements.Single().IsLastIterationSufficient_Helper(model, assume, iteratorVariable);
            var m = syntax.Statements().Min(e => e.IsLastIterationSufficient_Helper(model, assume, iteratorVariable));
            if (m != true) return m;
            var assigned = syntax.DescendantNodes()
                           .Where(e => e.Kind == SyntaxKind.AssignExpression)
                           .Cast<BinaryExpressionSyntax>()
                           .Select(e => e.Left)
                           .ToArray();
            var assignedSymbols = assigned.Select(e => model.GetSymbolInfo(e));
            var reads = syntax.DescendantNodes()
                        .Except(assigned)
                        .Select(e => model.GetSymbolInfo(e));
            if (!reads.Intersect(assignedSymbols).Any()) return true;
        }
        if (syntax is ExpressionStatementSyntax)
            return ((ExpressionStatementSyntax)syntax).Expression.IsLastIterationSufficient_Helper(model, assume, iteratorVariable);
        return null;
    }
    private static bool? IsLastIterationSufficient_Helper(this ExpressionSyntax syntax, ISemanticModel model, Assumptions assume, ISymbol iteratorVariable) {
        Contract.Requires(syntax != null);
        Contract.Requires(model != null);
        Contract.Requires(iteratorVariable != null);

        var isConst = syntax.IsConst(model);
        if (isConst == true) return true;
        if (syntax is IdentifierNameSyntax && model.GetSymbolInfo(syntax).Symbol == iteratorVariable) return true;
        if (syntax.Kind == SyntaxKind.AssignExpression) {
            var b = (BinaryExpressionSyntax)syntax;
            if (b.Left is IdentifierNameSyntax) {
                return b.Right.IsLastIterationSufficient_Helper2(model, assume, iteratorVariable);
            }
        }
        return null;
    }
    private static bool? IsLastIterationSufficient_Helper2(this ExpressionSyntax syntax, ISemanticModel model, Assumptions assume, ISymbol iteratorVariable) {
        Contract.Requires(syntax != null);
        Contract.Requires(model != null);
        Contract.Requires(iteratorVariable != null);

        var isConst = syntax.IsConst(model);
        if (isConst == true) return true;
        if (syntax is IdentifierNameSyntax && model.GetSymbolInfo(syntax).Symbol == iteratorVariable) return true;
        return null;
    }
    public static bool HasTopLevelIntraLoopJumps(this StatementSyntax syntax) {
        Contract.Requires(syntax != null);
        return syntax.DescendantNodesAndSelf(e => !e.IsLoopStatement()).Any(e => e.IsIntraLoopJump());
    }
    public static bool IsIntraLoopJump(this SyntaxNode syntax) {
        Contract.Requires(syntax != null);
        return syntax is BreakStatementSyntax 
            || syntax is ContinueStatementSyntax;
    }
    public static bool IsLoopStatement(this SyntaxNode syntax) {
        Contract.Requires(syntax != null);
        return syntax is StatementSyntax 
            && ((StatementSyntax)syntax).IsLoop();
    }
    public static bool IsLoop(this StatementSyntax syntax) {
        Contract.Requires(syntax != null);
        return syntax is ForEachStatementSyntax
            || syntax is ForStatementSyntax
            || syntax is WhileStatementSyntax
            || syntax is DoStatementSyntax;
    }

    public static StatementSyntax TryGetEquivalentJumpAfterStatement(this StatementSyntax node) {
        if (node.IsGuaranteedToJumpOut()) return null;
        SyntaxNode p = node;
        while (true) {
            p = p.Parent;
            if (p == null || p is MethodDeclarationSyntax)
                return Syntax.ReturnStatement(Syntax.Token(SyntaxKind.ReturnKeyword), null, Syntax.Token(SyntaxKind.SemicolonToken));
            if (p is WhileStatementSyntax || p is ForStatementSyntax)
                return Syntax.ContinueStatement(Syntax.Token(SyntaxKind.ContinueKeyword), Syntax.Token(SyntaxKind.SemicolonToken));
            if (p is BlockSyntax && (p as BlockSyntax).Statements().Last() != node)
                return null;
            if (p is StatementSyntax)
                return TryGetEquivalentJumpAfterStatement(p as StatementSyntax);
        }
    }

    public static IEnumerable<StatementSyntax> AppendUnlessJumps(this IEnumerable<StatementSyntax> statements, IEnumerable<StatementSyntax> tailStatements) {
        if (Syntax.Block(statements: Syntax.List(statements)).IsGuaranteedToJumpOut()) return statements;
        return statements.Concat(tailStatements);
    }

    private static bool? Max(this bool? v1, bool? v2) {
        if (v1 == true || v2 == true) return true;
        if (v1 == null || v2 == null) return null;
        return false;
    }
    private static bool? Min(this bool? v1, bool? v2) {
        if (v1 == false || v2 == false) return false;
        if (v1 == null || v2 == null) return null;
        return true;
    }
    public static TentativeBool HasSideEffects(this ExpressionSyntax expression, ISemanticModel model) {
        if (expression is IdentifierNameSyntax) return false;
        if (expression is LiteralExpressionSyntax) return false;
        if (expression is DefaultExpressionSyntax) return false;
        if (expression is MemberAccessExpressionSyntax) {
            var m = (MemberAccessExpressionSyntax)expression;
            return TentativeBool.ProbablyFalse.Max(m.Expression.HasSideEffects(model));
        }
        if (expression is InvocationExpressionSyntax) {
            var i = (InvocationExpressionSyntax)expression;
            if (i.ArgumentList.Arguments.Any(e => e.RefOrOutKeyword != null)) return true;
            return Enumerable.Max(i.ArgumentList.Arguments.Select(e => e.Expression.HasSideEffects(model)).Append(i.Expression.HasSideEffects(model), TentativeBool.Unknown));
        }
        if (expression is PostfixUnaryExpressionSyntax) {
            var u = (PostfixUnaryExpressionSyntax)expression;
            var unsafeOperators = new[] { SyntaxKind.PostDecrementExpression, SyntaxKind.PostIncrementExpression };
            if (unsafeOperators.Contains(u.Kind)) return true;
            return TentativeBool.Unknown;
        }
        if (expression is PrefixUnaryExpressionSyntax) {
            var u = (PrefixUnaryExpressionSyntax)expression;
            var shouldBeSafeOperators = new[] {
                SyntaxKind.LogicalNotExpression,
                SyntaxKind.BitwiseNotExpression,
                SyntaxKind.NegateExpression,
            };
            var unsafeOperators = new[] { SyntaxKind.PreDecrementExpression, SyntaxKind.PreIncrementExpression };
            if (unsafeOperators.Contains(u.Kind)) return true;
            var op = shouldBeSafeOperators.Contains(u.Kind) 
                   ? TentativeBool.ProbablyFalse 
                   : TentativeBool.Unknown;
            return op.Max(u.Operand.HasSideEffects(model));
        }
        if (expression is BinaryExpressionSyntax) {
            var b = (BinaryExpressionSyntax)expression;
            if (AssignmentOperatorKinds.Contains(b.Kind)) return true;
            var op = ProbablySafeBinaryOperators.Contains(b.Kind) 
                   ? TentativeBool.ProbablyFalse 
                   : TentativeBool.Unknown;
            return Enumerable.Max(new[] { op, b.Left.HasSideEffects(model), b.Right.HasSideEffects(model) });
        }
        if (expression is ParenthesizedExpressionSyntax) {
            return ((ParenthesizedExpressionSyntax)expression).Expression.HasSideEffects(model);
        }
        return TentativeBool.Unknown;
    }
    public static TentativeBool HasSideEffects(this StatementSyntax statement, ISemanticModel model) {
        if (statement is EmptyStatementSyntax) return false;
        var block = statement as BlockSyntax;
        if (statement is BlockSyntax) {
            if (((BlockSyntax)statement).Statements.Count == 0) return false;
            return Enumerable.Max(((BlockSyntax)statement).Statements.Select(e => e.HasSideEffects(model)));
        }
        if (statement is ExpressionStatementSyntax) {
            return ((ExpressionStatementSyntax)statement).Expression.HasSideEffects(model);
        }
        return TentativeBool.Unknown;
    }
    public static bool IsMoreThanPrivateGettable(this PropertyDeclarationSyntax syntax) {
        if (syntax.Modifiers.Any(e => e.Kind == SyntaxKind.PrivateKeyword)) return false; //property is private

        var getter = syntax.AccessorList.Accessors.FirstOrDefault(e => e.Kind == SyntaxKind.GetAccessorDeclaration);
        if (getter == null) return false; //no getter

        return !getter.Modifiers.Any(f => f.Kind == SyntaxKind.PrivateKeyword);
    }
    public static bool IsPublicGettable(this PropertyDeclarationSyntax syntax) {
        var accessKinds = new[] { SyntaxKind.PrivateKeyword, SyntaxKind.PublicKeyword, SyntaxKind.ProtectedKeyword, SyntaxKind.InternalKeyword };

        if (!syntax.Modifiers.Any(e => e.Kind == SyntaxKind.PublicKeyword)) return false; //property is not public

        var getter = syntax.AccessorList.Accessors.FirstOrDefault(e => e.Kind == SyntaxKind.GetAccessorDeclaration);
        if (getter == null) return false; //no getter

        Func<SyntaxKind, bool> hasAccessorType = t => getter.Modifiers.Any(f => f.Kind == t);
        if (hasAccessorType(SyntaxKind.PublicKeyword)) return true;

        // defaults to public if no access modifier
        return !accessKinds.Any(hasAccessorType);
    }
    public static bool IsPublicSettable(this PropertyDeclarationSyntax syntax) {
        var accessKinds = new[] { SyntaxKind.PrivateKeyword, SyntaxKind.PublicKeyword, SyntaxKind.ProtectedKeyword, SyntaxKind.InternalKeyword };

        if (!syntax.Modifiers.Any(e => e.Kind == SyntaxKind.PublicKeyword)) return false; //property is not public

        var setter = syntax.AccessorList.Accessors.FirstOrDefault(e => e.Kind == SyntaxKind.SetAccessorDeclaration);
        if (setter == null) return false; //no setter

        Func<SyntaxKind, bool> hasAccessorType = t => setter.Modifiers.Any(f => f.Kind == t);
        if (hasAccessorType(SyntaxKind.PublicKeyword)) return true;

        // defaults to public if no access modifier
        return !accessKinds.Any(hasAccessorType);
    }
    public static EqualsValueClauseSyntax NiceDefaultInitializer(this TypeSyntax syntax, ISemanticModel modelOpt, bool assumeImplicitConversion) {
        return Syntax.EqualsValueClause(value: syntax.NiceDefaultValueExpression(modelOpt, assumeImplicitConversion));
    }
    public static ExpressionSyntax NiceDefaultValueExpression(this TypeSyntax syntax, ISemanticModel modelOpt, bool assumeImplicitConversion) {
        Contract.Requires(syntax != null);

        var specialNumericTypes = new[] { 
            SpecialType.System_Byte, 
            SpecialType.System_Decimal, 
            SpecialType.System_Double, 
            SpecialType.System_Int16, 
            SpecialType.System_Int32, 
            SpecialType.System_Int64, 
            SpecialType.System_SByte, 
            SpecialType.System_Single, 
            SpecialType.System_UInt16, 
            SpecialType.System_UInt32, 
            SpecialType.System_UInt64 
        };

        if (modelOpt != null && assumeImplicitConversion) {
            var typeInfo = modelOpt.GetTypeInfo(syntax).ConvertedType;
            if (typeInfo.SpecialType == SpecialType.System_Boolean)
                return Syntax.LiteralExpression(SyntaxKind.FalseLiteralExpression, Syntax.Token(SyntaxKind.FalseKeyword));
            if (typeInfo.IsReferenceType || typeInfo.SpecialType == SpecialType.System_Nullable_T) 
                return Syntax.LiteralExpression(SyntaxKind.NullLiteralExpression, Syntax.Token(SyntaxKind.NullKeyword));
            if (specialNumericTypes.Contains(typeInfo.SpecialType)) 
                return Syntax.LiteralExpression(SyntaxKind.NumericLiteralExpression, Syntax.Literal("0", 0));
        }

        // 'default(T)'
        return Syntax.DefaultExpression(type: syntax);
    }
    public static bool IsSettable(this PropertyDeclarationSyntax syntax) {
        return syntax.AccessorList.Accessors.Any(e => e.Kind == SyntaxKind.SetAccessorDeclaration);
    }
    public static bool IsExactlyPrivateSettable(this PropertyDeclarationSyntax syntax) {
        var setter = syntax.AccessorList.Accessors.FirstOrDefault(e => e.Kind == SyntaxKind.SetAccessorDeclaration);
        if (setter == null) return false; //no setter

        return syntax.Modifiers.Concat(setter.Modifiers).Any(e => e.Kind == SyntaxKind.PrivateKeyword);
    }
    public static bool IsAutoProperty(this PropertyDeclarationSyntax syntax) {
        return syntax.AccessorList.Accessors.All(e => e.Body == null);
    }
    public static bool IsReadOnly(this FieldDeclarationSyntax syntax) {
        return syntax.Modifiers.Any(e => e.Kind == SyntaxKind.ReadOnlyKeyword);
    }
    public static bool IsInternalProtected(this FieldDeclarationSyntax syntax) {
        return syntax.IsProtected() && syntax.IsInternal();
    }
    /// <summary>Determines if the scope is protected (allows internal protected).</summary>
    public static bool IsProtected(this FieldDeclarationSyntax syntax) {
        return syntax.Modifiers.Any(e => e.Kind == SyntaxKind.ProtectedKeyword);
    }
    /// <summary>Determines if the scope is protected (does not allow internal protected).</summary>
    public static bool IsProtectedExactly(this FieldDeclarationSyntax syntax) {
        return syntax.IsProtected() && !syntax.IsInternal();
    }
    /// <summary>Determines if the scope is internal (allows internal protected).</summary>
    public static bool IsInternal(this FieldDeclarationSyntax syntax) {
        return syntax.Modifiers.Any(e => e.Kind == SyntaxKind.InternalKeyword);
    }
    /// <summary>Determines if the scope is internal (does not allow internal protected).</summary>
    public static bool IsInternalExactly(this FieldDeclarationSyntax syntax) {
        return syntax.IsInternal() && !syntax.IsProtected();
    }
    public static bool IsPrivate(this FieldDeclarationSyntax syntax) {
        if (syntax.Modifiers.Any(e => e.Kind == SyntaxKind.PrivateKeyword)) return true;
        return !syntax.IsProtected() && !syntax.IsPublic() && !syntax.IsInternal();
    }
    public static bool IsPublic(this FieldDeclarationSyntax syntax) {
        return syntax.Modifiers.Any(e => e.Kind == SyntaxKind.PublicKeyword);
    }
    public static bool IsStatic(this FieldDeclarationSyntax syntax) {
        return syntax.Modifiers.Any(e => e.Kind == SyntaxKind.StaticKeyword);
    }
    public static bool IsStatic(this PropertyDeclarationSyntax syntax) {
        return syntax.Modifiers.Any(e => e.Kind == SyntaxKind.StaticKeyword);
    }

    public static bool IsAssignment(this StatementSyntax syntax) {
        var b = syntax as ExpressionStatementSyntax;
        if (b == null) return false;
        return b.Expression.Kind == SyntaxKind.AssignExpression;
    }
    public static bool IsSingleInitialization(this StatementSyntax syntax) {
        var b = syntax as LocalDeclarationStatementSyntax;
        if (b == null) return false;
        return b.Declaration.Variables.Count == 1
            && b.Declaration.Variables.Single().Initializer != null;
    }
    public static bool IsAssignmentOrSingleInitialization(this StatementSyntax syntax) {
        return syntax is LocalDeclarationStatementSyntax || syntax.IsAssignment();
    }
    public static bool IsReturnValue(this StatementSyntax syntax) {
        var r = syntax as ReturnStatementSyntax;
        return r != null && r.Expression != null;
    }
    public static bool IsAssignmentOrSingleInitializationOrReturn(this StatementSyntax syntax) {
        return syntax.IsAssignmentOrSingleInitialization() || syntax.IsReturnValue();
    }
    public static ExpressionSyntax TryGetRHSOfAssignmentOrInit(this StatementSyntax syntax) {
        if (syntax.IsAssignment()) 
            return ((BinaryExpressionSyntax)((ExpressionStatementSyntax)syntax).Expression).Right;
        if (syntax.IsSingleInitialization()) 
            return ((LocalDeclarationStatementSyntax)syntax).Declaration.Variables.Single().Initializer.Value;
        return null;
    }
    public static ExpressionSyntax TryGetRHSOfAssignmentOrInitOrReturn(this StatementSyntax syntax) {
        if (syntax.IsReturnValue()) return ((ReturnStatementSyntax)syntax).Expression;
        return syntax.TryGetRHSOfAssignmentOrInit();
    }
    public static ISymbol TryGetLHSOfAssignmentOrInit(this StatementSyntax syntax, ISemanticModel model) {
        if (syntax.IsAssignment())
            return model.GetSymbolInfo(((BinaryExpressionSyntax)((ExpressionStatementSyntax)syntax).Expression).Left).Symbol;
        if (syntax.IsSingleInitialization())
            return model.GetSymbolInfo(((LocalDeclarationStatementSyntax)syntax).Declaration.Variables.Single()).Symbol;
        return null;
    }
    public static bool HasMatchingLHSOrRet(this StatementSyntax expression, StatementSyntax other, ISemanticModel model) {
        Contract.Requires(model != null);
        if (expression == null) return false;
        if (other == null) return false;
        if (expression.IsReturnValue() && other.IsReturnValue()) return true;
        var lhs1 = expression.TryGetLHSExpOfAssignmentOrInit();
        var lhs2 = other.TryGetLHSExpOfAssignmentOrInit();
        if (lhs1 == null || lhs2 == null) return false;
        return lhs1.IsMatchingLHS(lhs2, model);
    }
    public static ExpressionSyntax TryGetLHSExpOfAssignmentOrInit(this StatementSyntax syntax) {
         if (syntax.IsAssignment())
            return ((BinaryExpressionSyntax)((ExpressionStatementSyntax)syntax).Expression).Left;
         if (syntax.IsSingleInitialization())
            return Syntax.IdentifierName(((LocalDeclarationStatementSyntax)syntax).Declaration.Variables.Single().Identifier);
         return null;
    }
    public static bool IsMatchingLHS(this ExpressionSyntax lhs1, ExpressionSyntax lhs2, ISemanticModel model) {
        Contract.Requires(lhs1 != null);
        Contract.Requires(lhs2 != null);
        Contract.Requires(model != null);
        
        if (lhs1.Kind != lhs2.Kind) return false;
        if (!lhs1.HasSideEffects(model).IsProbablyFalse) return false;

        if (lhs1 is SimpleNameSyntax) return ((SimpleNameSyntax)lhs1).PlainName == ((SimpleNameSyntax)lhs2).PlainName;

        var s1 = model.GetSymbolInfo(lhs1);
        var s2 = model.GetSymbolInfo(lhs2);
        if (s1.Symbol != s2.Symbol) return false;

        var inv1 = lhs1 as InvocationExpressionSyntax;
        var inv2 = lhs2 as InvocationExpressionSyntax;
        if (inv1 != null) {
            return inv1.Expression.IsMatchingLHS(inv2.Expression, model) 
                && inv1.ArgumentList.Arguments.Count == inv2.ArgumentList.Arguments.Count 
                && inv1.ArgumentList.Arguments.Zip(
                        inv2.ArgumentList.Arguments, 
                        (e1, e2) => e1.NameColon == null 
                                && e2.NameColon == null 
                                && e1.Expression.IsMatchingLHS(e2.Expression, model)
                    ).All(e => e);
        }
        
        if (lhs1 is MemberAccessExpressionSyntax) 
            return ((MemberAccessExpressionSyntax)lhs1).Expression.IsMatchingLHS(((MemberAccessExpressionSyntax)lhs2).Expression, model);
        
        return false;
    }
    public static StatementSyntax ElseStatementOrEmptyBlock(this IfStatementSyntax syntax) {
        Contract.Requires(syntax != null);
        return syntax.Else != null ? syntax.Else.Statement : Syntax.Block();
    }
    public static IEnumerable<StatementSyntax> ElseAndFollowingStatements(this IfStatementSyntax syntax) {
        Contract.Requires(syntax != null);
        var parent = syntax.Parent as BlockSyntax;
        var alt = syntax.ElseStatementOrEmptyBlock().Statements();
        var following = parent != null ? parent.Statements.SkipWhile(e => e != syntax).Skip(1) : new StatementSyntax[0];
        return alt.Concat(following);
    }
    public static CommonSyntaxTree TryGetSyntaxTree(this IDocument document) {
        CommonSyntaxTree r;
        return document.TryGetSyntaxTree(out r) ? r : null;
    }

    public class ImplicitSingleStatementBranches {
        public readonly StatementSyntax True;
        public readonly StatementSyntax False;
        public readonly StatementSyntax ReplacePoint;
        public ImplicitSingleStatementBranches(StatementSyntax @true, StatementSyntax @false, StatementSyntax replacePoint) {
            this.True = @true;
            this.False = @false;
            this.ReplacePoint = replacePoint;
        }
        ///<summary>The statement whose RHS needs to be updated.</summary>
        public StatementSyntax Base { get { return False as LocalDeclarationStatementSyntax ?? True; } }
    }
    public static ImplicitSingleStatementBranches TryGetImplicitBranchSingleStatements(this IfStatementSyntax syntax, ISemanticModel model) {
        return TryGetIfStatementBranches_BothSingle(syntax) 
            ?? TryGetIfStatementBranches_ConditionalJump(syntax) 
            ?? TryGetIfStatementBranches_OverwritePrev(syntax, model);
    }
    public static ImplicitSingleStatementBranches TryGetIfStatementBranches_BothSingle(IfStatementSyntax syntax) {
        var trueAction = syntax.Statement.CollapsedStatements().SingleOrDefaultAllowMany();
        if (trueAction == null) return null;
        
        var falseAction = syntax.ElseStatementOrEmptyBlock().CollapsedStatements().SingleOrDefaultAllowMany();
        if (falseAction == null) return null;
        
        return new ImplicitSingleStatementBranches(trueAction, falseAction, syntax);
    }
    public static ImplicitSingleStatementBranches TryGetIfStatementBranches_ConditionalJump(IfStatementSyntax syntax) {
        var trueAction = syntax.Statement.CollapsedStatements().SingleOrDefaultAllowMany();
        if (trueAction == null) return null;
        if (!trueAction.IsGuaranteedToJumpOut()) return null;

        if (syntax.Else != null) return null;
        var followingAction = syntax.ElseAndFollowingStatements().FirstOrDefault();
        if (followingAction == null) return null;
        
        return new ImplicitSingleStatementBranches(trueAction, followingAction, syntax);
    }
    public static ImplicitSingleStatementBranches TryGetIfStatementBranches_OverwritePrev(IfStatementSyntax syntax, ISemanticModel model) {
        var trueAction = syntax.Statement.CollapsedStatements().SingleOrDefaultAllowMany();
        if (trueAction == null) return null;

        if (syntax.Else != null) return null;
        var prev = syntax.TryGetPrevStatement();
        if (prev == null) return null;

        if (trueAction.EffectsOverwriteEffectsOf(prev, model) != true) return null;
        return new ImplicitSingleStatementBranches(trueAction, prev, prev);
    }

    public static StatementSyntax TryGetPrevStatement(this StatementSyntax syntax) {
        Contract.Requires(syntax != null);
        var parent = syntax.Parent as BlockSyntax;
        if (parent == null) return null;
        return parent.Statements.TakeWhile(e => e != syntax).FirstOrDefault()
            ?? parent.TryGetPrevStatement();
    }
    ///<summary>Determines if the effects of executing the statement with/without the given previous statement are equivalent.</summary>
    public static bool? EffectsOverwriteEffectsOf(this StatementSyntax syntax, StatementSyntax prev, ISemanticModel model) {
        if (prev.IsGuaranteedToJumpOut()) return false;
        if (prev.HasSideEffects(model).IsProbablyFalse) return null;

        if (prev.IsAssignmentOrSingleInitialization()) {
            var rhs = prev.TryGetRHSOfAssignmentOrInit();
            if (rhs.HasSideEffects(model).IsProbablyFalse && syntax.HasMatchingLHSOrRet(prev, model))
                return true;
            return null;
        }
        return null;
    }
}
