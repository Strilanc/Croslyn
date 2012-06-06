using System.Linq;
using System.Threading;
using System.Windows.Media;
using Roslyn.Compilers.CSharp;
using Roslyn.Services;
using Roslyn.Services.Editor;
using System;
using System.Collections.Generic;

internal class ReadyCodeAction : ICodeAction {
    private readonly IDocument document;
    private readonly IEnumerable<SyntaxNode> oldNodes;
    private readonly Func<SyntaxNode, SyntaxNode, SyntaxNode> newNodeFunc;
    private readonly bool addFormatAnnotation;

    public string Description { get; private set; }
    public ImageSource Icon { get; private set; }

    public ReadyCodeAction(string desc, IDocument document, SyntaxNode oldNode, Func<SyntaxNode> newNodeFunc, bool addFormatAnnotation = true, ImageSource icon = null) {
        this.document = document;
        this.oldNodes = new[] {oldNode};
        this.newNodeFunc = (e, a) => newNodeFunc();
        this.Description = desc;
        this.Icon = icon;
        this.addFormatAnnotation = addFormatAnnotation;
    }
    public ReadyCodeAction(string desc, IDocument document, IEnumerable<SyntaxNode> oldNodes, Func<SyntaxNode, SyntaxNode, SyntaxNode> newNodeFunc, bool addFormatAnnotation = true, ImageSource icon = null) {
        this.document = document;
        this.oldNodes = oldNodes;
        this.newNodeFunc = newNodeFunc;
        this.Description = desc;
        this.Icon = icon;
        this.addFormatAnnotation = addFormatAnnotation;
    }

    public CodeActionEdit GetEdit(CancellationToken cancellationToken) {
        var tree = (SyntaxTree)document.GetSyntaxTree();
        var newRoot = tree.GetRoot().ReplaceNodes(
            oldNodes,
            (e, a) => {
                var n = newNodeFunc(e, a);
                if (addFormatAnnotation) n = CodeAnnotations.Formatting.AddAnnotationTo(n);
                return n;
            });
        return new CodeActionEdit(document.UpdateSyntaxRoot(newRoot));
    }
}
