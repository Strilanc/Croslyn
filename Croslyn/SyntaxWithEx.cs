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
using Roslyn.Services.Editor;
using Roslyn.Compilers;

public static class SyntaxWithEx {
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
        
        var newIfKeyword = ifKeyword ?? @this.IfKeyword;
        var newOpenParenToken = openParenToken ?? @this.OpenParenToken;
        var newCondition = condition ?? @this.Condition;
        var newCloseParenToken = closeParenToken ?? @this.CloseParenToken;
        var newStatement = statement ?? @this.Statement;
        var newElseOpt = elseOpt == null ? @this.ElseOpt : elseOpt.Value;
        
        if (newIfKeyword == @this.IfKeyword
                && newOpenParenToken == @this.OpenParenToken
                && newCondition == @this.Condition
                && newCloseParenToken == @this.CloseParenToken
                && newStatement == @this.Statement
                && newElseOpt == @this.ElseOpt)
            return @this;

        return @this.Update(newIfKeyword,
                            newOpenParenToken,
                            newCondition,
                            newCloseParenToken,
                            newStatement,
                            newElseOpt);
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
    public static BinaryExpressionSyntax With(this BinaryExpressionSyntax syntax,
                                              ExpressionSyntax left = null,
                                              SyntaxToken? operatorToken = null,
                                              ExpressionSyntax right = null) {
        return syntax.Update(left ?? syntax.Left, operatorToken ?? syntax.OperatorToken, right ?? syntax.Right);
    }
    public static ExpressionStatementSyntax With(this ExpressionStatementSyntax syntax,
                                                 ExpressionSyntax expression = null,
                                                 SyntaxToken? semicolonToken = null) {
        return syntax.Update(expression ?? syntax.Expression, semicolonToken ?? syntax.SemicolonToken);
    }
    public static VariableDeclarationSyntax With(this VariableDeclarationSyntax syntax,
                                                 TypeSyntax type = null,
                                                 SeparatedSyntaxList<VariableDeclaratorSyntax>? variables = null) {
        return syntax.Update(type ?? syntax.Type, variables ?? syntax.Variables);
    }
    public static LocalDeclarationStatementSyntax With(this LocalDeclarationStatementSyntax syntax,
                                                       SyntaxTokenList? modifiers = null,
                                                       VariableDeclarationSyntax declaration = null,
                                                       SyntaxToken? semicolonToken = null) {
        return syntax.Update(modifiers ?? syntax.Modifiers, declaration ?? syntax.Declaration, semicolonToken ?? syntax.SemicolonToken);
    }
    public static VariableDeclaratorSyntax With(this VariableDeclaratorSyntax syntax,
                                                SyntaxToken? identifier = null,
                                                Renullable<BracketedArgumentListSyntax> argumentListOpt = null,
                                                Renullable<EqualsValueClauseSyntax> initializerOpt = null) {
        return syntax.Update(identifier ?? syntax.Identifier, 
                             argumentListOpt == null ? syntax.ArgumentListOpt : argumentListOpt.Value, 
                             initializerOpt == null ? syntax.InitializerOpt : initializerOpt.Value);
    }
    public static ParameterListSyntax With(this ParameterListSyntax syntax,
                                           SyntaxToken? openParenToken = null,
                                           SeparatedSyntaxList<ParameterSyntax>? parameters = null,
                                           SyntaxToken? closeParenToken = null) {
        return syntax.Update(openParenToken ?? syntax.OpenParenToken,
                             parameters ?? syntax.Parameters,
                             closeParenToken ?? syntax.CloseParenToken);
    }
    public static ArgumentListSyntax With(this ArgumentListSyntax syntax,
                                           SyntaxToken? openParenToken = null,
                                           SeparatedSyntaxList<ArgumentSyntax>? arguments = null,
                                           SyntaxToken? closeParenToken = null) {
        return syntax.Update(openParenToken ?? syntax.OpenParenToken,
                             arguments ?? syntax.Arguments,
                             closeParenToken ?? syntax.CloseParenToken);
    }
    public static InitializerExpressionSyntax With(this InitializerExpressionSyntax syntax,
                                                   SyntaxToken? openBraceToken = null,
                                                   SeparatedSyntaxList<ExpressionSyntax>? expressions = null,
                                                   SyntaxToken? closeBraceToken = null) {
        return syntax.Update(openBraceToken ?? syntax.OpenBraceToken,
                             expressions ?? syntax.Expressions,
                             closeBraceToken ?? syntax.CloseBraceToken);
    }
    public static ForEachStatementSyntax With(this ForEachStatementSyntax syntax,
                                              SyntaxToken? forEachKeyword = null,
                                              SyntaxToken? openParenToken = null,
                                              TypeSyntax type = null,
                                              SyntaxToken? identifier = null,
                                              SyntaxToken? inKeyword = null,
                                              ExpressionSyntax expression = null,
                                              SyntaxToken? closeParenToken = null,
                                              StatementSyntax statement = null) {
        return syntax.Update(forEachKeyword ?? syntax.ForEachKeyword,
                             openParenToken ?? syntax.OpenParenToken,
                             type ?? syntax.Type,
                             identifier ?? syntax.Identifier,
                             inKeyword ?? syntax.InKeyword,
                             expression ?? syntax.Expression,
                             closeParenToken ?? syntax.CloseParenToken,
                             statement ?? syntax.Statement);
    }
    public static ReturnStatementSyntax With(this ReturnStatementSyntax syntax,
                                             SyntaxToken? returnKeyword = null,
                                             Renullable<ExpressionSyntax> expressionOpt = null,
                                             SyntaxToken? semicolonToken = null) {
        return syntax.Update(returnKeyword ?? syntax.ReturnKeyword,
                             expressionOpt == null ? syntax.ExpressionOpt : expressionOpt.Value,
                             semicolonToken ?? syntax.SemicolonToken);
    }
    public static QueryExpressionSyntax With(this QueryExpressionSyntax syntax,
                                             SyntaxList<QueryClauseSyntax>? clauses = null,
                                             SelectOrGroupClauseSyntax selectOrGroup = null,
                                             Renullable<QueryContinuationSyntax> continuationOpt = null) {
        return syntax.Update(clauses ?? syntax.Clauses,
                             selectOrGroup ?? syntax.SelectOrGroup,
                             continuationOpt == null ? syntax.ContinuationOpt : continuationOpt.Value);
    }
    public static SeparatedSyntaxList<T> With<T>(this SeparatedSyntaxList<T> syntax,
                                                 IEnumerable<T> nodes = null,
                                                 IEnumerable<SyntaxToken> seps = null) where T : SyntaxNode {
        return Syntax.SeparatedList(nodes ?? syntax.AsEnumerable(),
                                    seps ?? syntax.Seperators());
    }
    public static SyntaxList<T> With<T>(this SyntaxList<T> syntax,
                                        IEnumerable<T> nodes = null) where T : SyntaxNode {
        return nodes == null ? syntax : Syntax.List(nodes);
    }
    public static FieldDeclarationSyntax With(this FieldDeclarationSyntax syntax,
                                              SyntaxList<AttributeDeclarationSyntax>? attributes = null,
                                              SyntaxTokenList? modifiers = null,
                                              VariableDeclarationSyntax declaration = null,
                                              SyntaxToken? semicolonToken = null) {
        return syntax.Update(attributes ?? syntax.Attributes,
                             modifiers ?? syntax.Modifiers,
                             declaration ?? syntax.Declaration,
                             semicolonToken ?? syntax.SemicolonToken);
    }
}
