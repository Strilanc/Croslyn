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

    public static bool IsGuaranteedToJumpOut(this StatementSyntax node) {
        Contract.Requires(node != null);
        if (node is ReturnStatementSyntax) return true;
        if (node is ThrowStatementSyntax) return true;
        if (node is BreakStatementSyntax) return true;
        if (node is ContinueStatementSyntax) return true;
        var i = node as IfStatementSyntax;
        if (i != null) return i.ElseOpt != null && i.Statement.IsGuaranteedToJumpOut() && i.ElseOpt.Statement.IsGuaranteedToJumpOut();
        var b = node as BlockSyntax;
        if (b != null) return b.Statements.Count > 0 && b.Statements.Last().IsGuaranteedToJumpOut();
        return false;
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

    public enum Result : int {
        False = -2,
        FalseIfCodeFollowsConventions = -1,
        Unknown = 0,
        TrueIfCodeFollowsConventions = 1,
        True = 2
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
                SyntaxKind.GreaterThanOrEqualExpression
            };
            if (!shouldBeSafeOperators.Contains(b.Kind)) return Result.Unknown;
            return Result.FalseIfCodeFollowsConventions.Max(b.Left.HasSideEffects().Max(b.Right.HasSideEffects()));
        }
        if (expression is ParenthesizedExpressionSyntax) {
            return ((ParenthesizedExpressionSyntax)expression).Expression.HasSideEffects();
        }
        return Result.Unknown;
    }
    public static Result HasSideEffects(this StatementSyntax statement, ISemanticModel model = null) {
        if (statement is EmptyStatementSyntax) return Result.True;
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
}
