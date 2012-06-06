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
}
