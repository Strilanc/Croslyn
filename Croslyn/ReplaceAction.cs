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

public class ReplaceAction {
    public readonly string Description;
    public readonly SyntaxNode OldNode;
    public readonly SyntaxNode NewNode;
    public ReplaceAction(string description, SyntaxNode oldNode, SyntaxNode newNode) {
        this.Description = description;
        this.OldNode = oldNode;
        this.NewNode = newNode;
    }
    public ICodeAction AsCodeAction(IDocument document) {
        return new ReadyCodeAction(Description, document, OldNode, () => NewNode);
    }
}
