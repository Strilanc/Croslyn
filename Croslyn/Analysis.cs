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
    ///<summary>The statements in a block statement or, if not a block statement, the single statement.</summary>
    public static StatementSyntax[] Statements(this StatementSyntax e) {
        return e is BlockSyntax ? ((BlockSyntax)e).Statements.ToArray() : new[] { e };
    }

    public static IEnumerable<IdentifierNameSyntax> ReadsOfLocalVariable(this SyntaxNode scope, IdentifierNameSyntax localVar) {
        return scope.ReadsOfLocalVariable(localVar);
    }
    public static IEnumerable<IdentifierNameSyntax> ReadsOfLocalVariable(this SyntaxNode scope, SyntaxToken localVar) {
        return scope.DescendentNodes()
               .OfType<IdentifierNameSyntax>()
               .Where(e => e.Identifier.ValueText == localVar.ValueText);
    }

    public static String TryGetCodeBlockOrAreaDescription(this SyntaxNode e) {
        if (e is ClassDeclarationSyntax) return "Class";
        if (e is MethodDeclarationSyntax) return ((MethodDeclarationSyntax)e).BodyOpt == null ? null : "Method";
        if (e is StructDeclarationSyntax) return "Struct";
        if (e is BlockSyntax) return "Block";
        if (e is IfStatementSyntax) return "If Block";
        if (e is WhileStatementSyntax) return "While Loop";
        if (e is ForStatementSyntax) return "For Loop";
        if (e is NamespaceDeclarationSyntax) return "Namespace";
        return null;
    }

    public static bool IsGuaranteedToJumpOut(this StatementSyntax node, bool includeContinue = true) {
        Contract.Requires(node != null);
        if (node is ReturnStatementSyntax) return true;
        if (node is ThrowStatementSyntax) return true;
        if (node is BreakStatementSyntax) return true;
        if (node is ContinueStatementSyntax) return includeContinue;
        var i = node as IfStatementSyntax;
        if (i != null) return i.ElseOpt != null && i.Statement.IsGuaranteedToJumpOut(includeContinue) && i.ElseOpt.Statement.IsGuaranteedToJumpOut(includeContinue);
        var b = node as BlockSyntax;
        if (b != null) return b.Statements.Count > 0 && b.Statements.Last().IsGuaranteedToJumpOut(includeContinue);
        return false;
    }
    public static Result IsLoopVarFirstpotent(this ExpressionSyntax syntax, IEnumerable<ExpressionSyntax> loopVarReads, ISemanticModel model = null) {
        if (loopVarReads.Contains(syntax)) return Result.False;
        if (syntax is LiteralExpressionSyntax) return Result.True;
        if (syntax.Kind == SyntaxKind.AssignExpression) {
            var b = (BinaryExpressionSyntax)syntax;
            if (b.Left is IdentifierNameSyntax) return b.Right.IsLoopVarFirstpotent(loopVarReads, model);
        }
        return Result.Unknown;
    }
    public static Result IsLoopVarFirstpotent(this StatementSyntax syntax, IEnumerable<ExpressionSyntax> loopVarReads, ISemanticModel model = null) {
        if (syntax is BlockSyntax) {
            if (syntax.IsGuaranteedToJumpOut(includeContinue: false)) return Result.True;
            return syntax.Statements().Min(e => e.IsLoopVarFirstpotent(loopVarReads, model));
        }
        if (syntax is ReturnStatementSyntax) return Result.True;
        if (syntax is BreakStatementSyntax) return Result.True;
        if (syntax is ContinueStatementSyntax) return Result.True;
        if (syntax is ThrowStatementSyntax) return Result.True;
        if (syntax is ExpressionStatementSyntax) return ((ExpressionStatementSyntax)syntax).Expression.IsLoopVarFirstpotent(loopVarReads, model);
        return Result.Unknown;
    }
    public static Result IsLoopVarLastpotent(this ExpressionSyntax syntax, IEnumerable<ExpressionSyntax> loopVarReads, ISemanticModel model = null) {
        if (loopVarReads.Contains(syntax)) return Result.True;
        if (syntax is LiteralExpressionSyntax) return Result.True;
        if (syntax.Kind == SyntaxKind.AssignExpression) {
            var b = (BinaryExpressionSyntax)syntax;
            if (b.Left is IdentifierNameSyntax) return b.Right.IsLoopVarLastpotent(loopVarReads, model);
        }
        return Result.Unknown;
    }
    public static Result IsLoopVarLastpotent(this StatementSyntax syntax, IEnumerable<ExpressionSyntax> loopVarReads, ISemanticModel model = null) {
        if (syntax is BlockSyntax) {
            if (syntax.IsGuaranteedToJumpOut(includeContinue: false)) return Result.False;
            return syntax.Statements().Min(e => e.IsLoopVarLastpotent(loopVarReads, model));
        }
        if (syntax is ReturnStatementSyntax) return Result.False;
        if (syntax is BreakStatementSyntax) return Result.False;
        if (syntax is ThrowStatementSyntax) return Result.False;
        if (syntax is ContinueStatementSyntax) return Result.True;
        if (syntax is ExpressionStatementSyntax) return ((ExpressionStatementSyntax)syntax).Expression.IsLoopVarLastpotent(loopVarReads, model);
        return Result.Unknown;
    }
    public static bool HasTopLevelIntraLoopJumps(this StatementSyntax syntax) {
        Contract.Requires(syntax != null);
        return syntax.DescendentNodesAndSelf(e => e.IsLoopStatement()).Any(e => e.IsIntraLoopJump());
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

    public enum Result : int {
        False = -2,
        FalseIfCodeFollowsConventions = -1,
        Unknown = 0,
        TrueIfCodeFollowsConventions = 1,
        True = 2
    }
    public static Result Invert(this Result r) {
        return (Result)(-(int)r);
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
    private static Result Max(this Result v1, Result v2) {
        return (Result)Math.Max((int)v1, (int)v2);
    }
    private static Result Min(this Result v1, Result v2) {
        return (Result)Math.Min((int)v1, (int)v2);
    }

    public static Result HasSideEffects(this ExpressionSyntax expression, ISemanticModel model = null) {
        if (expression is LiteralExpressionSyntax) return Result.False;
        if (expression is DefaultExpressionSyntax) return Result.False;
        if (expression is MemberAccessExpressionSyntax) {
            var m = (MemberAccessExpressionSyntax)expression;
            return Result.FalseIfCodeFollowsConventions.Max(m.Expression.HasSideEffects(model));
        }
        if (expression is InvocationExpressionSyntax) {
            var i = (InvocationExpressionSyntax)expression;
            if (i.ArgumentList.Arguments.Any(e => e.RefOrOutKeywordOpt != null)) return Result.True;
            return Enumerable.Max(i.ArgumentList.Arguments.Select(e => e.Expression.HasSideEffects(model)).Append(i.Expression.HasSideEffects(model), Result.Unknown));
        }
        if (expression is PrefixUnaryExpressionSyntax) {
            var u = (PrefixUnaryExpressionSyntax)expression;
            var shouldBeSafeOperators = new SyntaxKind[] {
                SyntaxKind.LogicalNotExpression,
                SyntaxKind.BitwiseNotExpression,
                SyntaxKind.NegateExpression,
            };
            var op = shouldBeSafeOperators.Contains(u.Kind) ? Result.FalseIfCodeFollowsConventions : Result.Unknown;
            return op.Max(u.Operand.HasSideEffects(model));
        }
        if (expression is BinaryExpressionSyntax) {
            var b = (BinaryExpressionSyntax)expression;
            var shouldBeSafeOperators = new SyntaxKind[] {
                SyntaxKind.EqualsExpression,
                SyntaxKind.NotEqualsExpression,
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
            var unsafeOperators = new SyntaxKind[] {
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
            if (unsafeOperators.Contains(b.Kind)) return Result.True;
            var op = shouldBeSafeOperators.Contains(b.Kind) ? Result.FalseIfCodeFollowsConventions : Result.Unknown;
            return Enumerable.Max(new[] { op, b.Left.HasSideEffects(model), b.Right.HasSideEffects(model) });
        }
        if (expression is ParenthesizedExpressionSyntax) {
            return ((ParenthesizedExpressionSyntax)expression).Expression.HasSideEffects(model);
        }
        return Result.Unknown;
    }
    public static Result HasSideEffects(this StatementSyntax statement, ISemanticModel model = null) {
        if (statement is EmptyStatementSyntax) return Result.False;
        var block = statement as BlockSyntax;
        if (statement is BlockSyntax) {
            if (((BlockSyntax)statement).Statements.Count == 0) return Result.False;
            return ((BlockSyntax)statement).Statements.Select(e => e.HasSideEffects()).MaxBy(e => (int)e);
        }
        if (statement is ExpressionStatementSyntax) {
            return ((ExpressionStatementSyntax)statement).Expression.HasSideEffects();
        }
        return Result.Unknown;
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
    public static EqualsValueClauseSyntax NiceDefaultInitializer(this TypeSyntax syntax, ISemanticModel modelOpt) {
        return Syntax.EqualsValueClause(value: syntax.NiceDefaultValueExpression(modelOpt));
    }
    public static ExpressionSyntax NiceDefaultValueExpression(this TypeSyntax syntax, ISemanticModel modelOpt) {
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

        if (modelOpt != null) {
            var typeInfo = modelOpt.GetSemanticInfo(syntax).ConvertedType;
            if (typeInfo.SpecialType == SpecialType.System_Boolean)
                return Syntax.LiteralExpression(SyntaxKind.FalseLiteralExpression, Syntax.Token(SyntaxKind.FalseKeyword));
            if (typeInfo.IsReferenceType || typeInfo.SpecialType == SpecialType.System_Nullable_T) 
                return Syntax.LiteralExpression(SyntaxKind.NullLiteralExpression, Syntax.Token(SyntaxKind.NullKeyword));
            if (specialNumericTypes.Contains(typeInfo.SpecialType)) 
                return Syntax.LiteralExpression(SyntaxKind.NumericLiteralExpression, Syntax.Literal("0", 0));
        }

        // 'default(T)'
        return Syntax.DefaultExpression(argumentList: Syntax.ArgumentList(arguments: Syntax.SeparatedList(Syntax.Argument(expression: syntax))));
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
        return syntax.AccessorList.Accessors.All(e => e.BodyOpt == null);
    }
    public static bool IsReadOnly(this FieldDeclarationSyntax syntax) {
        return syntax.Modifiers.Any(e => e.Kind == SyntaxKind.ReadOnlyKeyword);
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
            && b.Declaration.Variables.Single().InitializerOpt != null;
    }
    public static bool IsAssignmentOrSingleInitialization(this StatementSyntax syntax) {
        return syntax is LocalDeclarationStatementSyntax || syntax.IsAssignment();
    }
    public static bool IsReturnValue(this StatementSyntax syntax) {
        var r = syntax as ReturnStatementSyntax;
        return r != null && r.ExpressionOpt != null;
    }
    public static bool IsAssignmentOrSingleInitializationOrReturn(this StatementSyntax syntax) {
        return syntax.IsAssignmentOrSingleInitialization() || syntax.IsReturnValue();
    }
    public static ExpressionSyntax TryGetRightHandSideOfAssignmentOrSingleInit(this StatementSyntax syntax) {
        if (syntax.IsAssignment()) 
            return ((BinaryExpressionSyntax)((ExpressionStatementSyntax)syntax).Expression).Right;
        if (syntax.IsSingleInitialization()) 
            return ((LocalDeclarationStatementSyntax)syntax).Declaration.Variables.Single().InitializerOpt.Value;
        return null;
    }
    public static ExpressionSyntax TryGetRightHandSideOfAssignmentOrSingleInitOrReturnValue(this StatementSyntax syntax) {
        if (syntax.IsReturnValue()) return ((ReturnStatementSyntax)syntax).ExpressionOpt;
        return syntax.TryGetRightHandSideOfAssignmentOrSingleInit();
    }
    public static ExpressionSyntax TryGetLeftHandSideOfAssignmentOrSingleInit(this StatementSyntax syntax) {
        if (syntax.IsAssignment())
            return ((BinaryExpressionSyntax)((ExpressionStatementSyntax)syntax).Expression).Left;
        if (syntax.IsSingleInitialization())
            return Syntax.IdentifierName(((LocalDeclarationStatementSyntax)syntax).Declaration.Variables.Single().Identifier);
        return null;
    }
    public static StatementSyntax ElseStatementOrEmptyBlock(this IfStatementSyntax syntax) {
        Contract.Requires(syntax != null);
        return syntax.ElseOpt != null ? syntax.ElseOpt.Statement : Syntax.Block();
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
}
