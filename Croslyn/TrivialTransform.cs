using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Roslyn.Compilers.CSharp;
using System.Diagnostics.Contracts;
using Roslyn.Services;
using Roslyn.Compilers.Common;
using Strilbrary.Collections;
using Strilbrary.Values;

public static class TrivialTransforms {
    public static TypeDeclarationSyntax With(this TypeDeclarationSyntax @this,
                                             SyntaxList<AttributeDeclarationSyntax>? attributes = null,
                                             SyntaxTokenList? modifiers = null,
                                             SyntaxToken? keyword = null,
                                             SyntaxToken? identifier = null,
                                             Renullable<TypeParameterListSyntax> typeParameterListOpt = null,
                                             BaseListSyntax baseListOpt = null,
                                             SyntaxList<TypeParameterConstraintClauseSyntax>? constraintClauses = null,
                                             SyntaxToken? openBraceToken = null,
                                             SyntaxList<MemberDeclarationSyntax>? members = null,
                                             SyntaxToken? closeBraceToken = null,
                                             SyntaxToken? semicolonTokenOpt = null) {
        Contract.Requires(@this != null);
        var itf = @this as InterfaceDeclarationSyntax;
        if (itf != null) return itf.With(attributes, modifiers, keyword, identifier, typeParameterListOpt, baseListOpt, constraintClauses, openBraceToken, members, closeBraceToken, semicolonTokenOpt);
        var cls = @this as ClassDeclarationSyntax;
        if (cls != null) return cls.With(attributes, modifiers, keyword, identifier, typeParameterListOpt, baseListOpt, constraintClauses, openBraceToken, members, closeBraceToken, semicolonTokenOpt);
        var stc = @this as StructDeclarationSyntax;
        if (stc != null) return stc.With(attributes, modifiers, keyword, identifier, typeParameterListOpt, baseListOpt, constraintClauses, openBraceToken, members, closeBraceToken, semicolonTokenOpt);
        throw new NotSupportedException("Unrecognized TypeDeclarationSyntax type.");
    }
    public static InterfaceDeclarationSyntax With(this InterfaceDeclarationSyntax @this,
                                                  SyntaxList<AttributeDeclarationSyntax>? attributes = null,
                                                  SyntaxTokenList? modifiers = null,
                                                  SyntaxToken? keyword = null,
                                                  SyntaxToken? identifier = null,
                                                  Renullable<TypeParameterListSyntax> typeParameterListOpt = null,
                                                  BaseListSyntax baseListOpt = null,
                                                  SyntaxList<TypeParameterConstraintClauseSyntax>? constraintClauses = null,
                                                  SyntaxToken? openBraceToken = null,
                                                  SyntaxList<MemberDeclarationSyntax>? members = null,
                                                  SyntaxToken? closeBraceToken = null,
                                                  SyntaxToken? semicolonTokenOpt = null) {
        Contract.Requires(@this != null);
        var r = @this.Update(attributes ?? @this.Attributes,
                             modifiers ?? @this.Modifiers,
                             keyword ?? @this.Keyword,
                             identifier ?? @this.Identifier,
                             typeParameterListOpt == null ? @this.TypeParameterListOpt : typeParameterListOpt.Value,
                             baseListOpt ?? @this.BaseListOpt,
                             constraintClauses ?? @this.ConstraintClauses,
                             openBraceToken ?? @this.OpenBraceToken,
                             members ?? @this.Members,
                             closeBraceToken ?? @this.CloseBraceToken,
                             semicolonTokenOpt ?? @this.SemicolonTokenOpt);
        return r == @this ? @this : r;
    }
    public static ClassDeclarationSyntax With(this ClassDeclarationSyntax @this,
                                              SyntaxList<AttributeDeclarationSyntax>? attributes = null,
                                              SyntaxTokenList? modifiers = null,
                                              SyntaxToken? keyword = null,
                                              SyntaxToken? identifier = null,
                                              Renullable<TypeParameterListSyntax> typeParameterListOpt = null,
                                              BaseListSyntax baseListOpt = null,
                                              SyntaxList<TypeParameterConstraintClauseSyntax>? constraintClauses = null,
                                              SyntaxToken? openBraceToken = null,
                                              SyntaxList<MemberDeclarationSyntax>? members = null,
                                              SyntaxToken? closeBraceToken = null,
                                              SyntaxToken? semicolonTokenOpt = null) {
        Contract.Requires(@this != null);
        var r = @this.Update(attributes ?? @this.Attributes,
                             modifiers ?? @this.Modifiers,
                             keyword ?? @this.Keyword,
                             identifier ?? @this.Identifier,
                             typeParameterListOpt == null ? @this.TypeParameterListOpt : typeParameterListOpt.Value,
                             baseListOpt ?? @this.BaseListOpt,
                             constraintClauses ?? @this.ConstraintClauses,
                             openBraceToken ?? @this.OpenBraceToken,
                             members ?? @this.Members,
                             closeBraceToken ?? @this.CloseBraceToken,
                             semicolonTokenOpt ?? @this.SemicolonTokenOpt);
        return r == @this ? @this : r;
    }
    public static StructDeclarationSyntax With(this StructDeclarationSyntax @this,
                                               SyntaxList<AttributeDeclarationSyntax>? attributes = null,
                                               SyntaxTokenList? modifiers = null,
                                               SyntaxToken? keyword = null,
                                               SyntaxToken? identifier = null,
                                               Renullable<TypeParameterListSyntax> typeParameterListOpt = null,
                                               BaseListSyntax baseListOpt = null,
                                               SyntaxList<TypeParameterConstraintClauseSyntax>? constraintClauses = null,
                                               SyntaxToken? openBraceToken = null,
                                               SyntaxList<MemberDeclarationSyntax>? members = null,
                                               SyntaxToken? closeBraceToken = null,
                                               SyntaxToken? semicolonTokenOpt = null) {
        Contract.Requires(@this != null);
        var r = @this.Update(attributes ?? @this.Attributes,
                             modifiers ?? @this.Modifiers,
                             keyword ?? @this.Keyword,
                             identifier ?? @this.Identifier,
                             typeParameterListOpt == null ? @this.TypeParameterListOpt : typeParameterListOpt.Value,
                             baseListOpt ?? @this.BaseListOpt,
                             constraintClauses ?? @this.ConstraintClauses,
                             openBraceToken ?? @this.OpenBraceToken,
                             members ?? @this.Members,
                             closeBraceToken ?? @this.CloseBraceToken,
                             semicolonTokenOpt ?? @this.SemicolonTokenOpt);
        return r == @this ? @this : r;
    }
    public static IfStatementSyntax With(this IfStatementSyntax @this,
                                         SyntaxToken? ifKeyword = null,
                                         SyntaxToken? openParenToken = null,
                                         ExpressionSyntax condition = null,
                                         SyntaxToken? closeParenToken = null,
                                         StatementSyntax statement = null,
                                         Renullable<ElseClauseSyntax> elseOpt = null) {
        Contract.Requires(@this != null);
        var r = @this.Update(ifKeyword ?? @this.IfKeyword,
                             openParenToken ?? @this.OpenParenToken ,
                             condition ?? @this.Condition,
                             closeParenToken ?? @this.CloseParenToken,
                             statement ?? @this.Statement,
                             elseOpt == null ? @this.ElseOpt : elseOpt.Value);
        return r == @this ? @this : r;
    }
    public static WhileStatementSyntax With(this WhileStatementSyntax @this,
                                            SyntaxToken? whileKeyword = null,
                                            SyntaxToken? openParenToken = null,
                                            ExpressionSyntax condition = null,
                                            SyntaxToken? closeParenToken = null,
                                            StatementSyntax statement = null) {
        Contract.Requires(@this != null);
        var r = @this.Update(whileKeyword ?? @this.WhileKeyword,
                             openParenToken ?? @this.OpenParenToken,
                             condition ?? @this.Condition,
                             closeParenToken ?? @this.CloseParenToken,
                             statement ?? @this.Statement);
        return r == @this ? @this : r;
    }
    public static BlockSyntax With(this BlockSyntax @this,
                                   SyntaxToken? openBraceToken = null,
                                   SyntaxList<StatementSyntax>? statements = null,
                                   SyntaxToken? closeBraceToken = null) {
        Contract.Requires(@this != null);
        var r = @this.Update(openBraceToken ?? @this.OpenBraceToken,
                             statements ?? @this.Statements,
                             closeBraceToken ?? @this.CloseBraceToken);
        return r == @this ? @this : r;
    }

    public static BlockSyntax Block(this IEnumerable<StatementSyntax> statements) {
        Contract.Requires(statements != null);
        return Syntax.Block(statements: Syntax.List(statements));
    }
    public static SyntaxList<T> List<T>(this IEnumerable<T> items) where T : SyntaxNode {
        return Syntax.List(items);
    }
    public static SyntaxList<T> List1<T>(this T item) where T : SyntaxNode {
        return Syntax.List(item);
    }
    public static SeparatedSyntaxList<T> SepList1<T>(this T item) where T : SyntaxNode {
        return Syntax.SeparatedList(item);
    }
    public static ArgumentSyntax Arg(this ExpressionSyntax item) {
        return Syntax.Argument(expression: item);
    }
    public static ArgumentListSyntax Args1(this ExpressionSyntax item) {
        return Syntax.ArgumentList(arguments: item.Arg().SepList1());
    }
    public static TypeArgumentListSyntax TypeArgs1(this TypeSyntax syntax) {
        return Syntax.TypeArgumentList(arguments: Syntax.SeparatedList(syntax));
    }
    public static GenericNameSyntax Genericed(this SyntaxToken identifier, TypeSyntax genericType1) {
        return Syntax.GenericName(identifier, genericType1.TypeArgs1());
    }
    public static NullableTypeSyntax Nullable(this TypeSyntax t) {
        return Syntax.NullableType(t);
    }
    public static MemberAccessExpressionSyntax Accessing(this ExpressionSyntax instance, SimpleNameSyntax member) {
        return Syntax.MemberAccessExpression(SyntaxKind.MemberAccessExpression, instance, name: member);
    }
    public static MemberAccessExpressionSyntax Accessing(this ExpressionSyntax instance, String name) {
        return Syntax.MemberAccessExpression(SyntaxKind.MemberAccessExpression, instance, name: name.AsIdentifier());
    }
    public static InvocationExpressionSyntax Invoking(this ExpressionSyntax target, ArgumentListSyntax args = null) {
        return Syntax.InvocationExpression(target, args ?? Syntax.ArgumentList());
    }
    public static SimpleLambdaExpressionSyntax Lambdad(this SyntaxToken parameter, ExpressionSyntax body) {
        return Syntax.SimpleLambdaExpression(Syntax.Parameter(identifier: parameter), body: body);
    }
    public static SimpleNameSyntax AsIdentifier(this String name) {
        return Syntax.IdentifierName(name);
    }
    public static LocalDeclarationStatementSyntax varInit(this SyntaxToken name, ExpressionSyntax value) {
        return Syntax.LocalDeclarationStatement(declaration: Syntax.VariableDeclaration(
            Syntax.IdentifierName(Syntax.Token(SyntaxKind.VarKeyword)),
            Syntax.VariableDeclarator(
                name,
                initializerOpt: Syntax.EqualsValueClause(
                    value: value)).SepList1()));
    }
}
