using System.Linq;
using System.Threading;
using System.Windows.Media;
using Roslyn.Compilers.CSharp;
using Roslyn.Services;
using Roslyn.Services.Editor;
using System;
using System.Collections.Generic;

internal class ReadyCodeAction : ICodeAction {
    private readonly ICodeActionEditFactory editFactory;
    private readonly IDocument document;
    private readonly IEnumerable<SyntaxNode> oldNodes;
    private readonly Func<SyntaxNode, SyntaxNode, SyntaxNode> newNodeFunc;
    private readonly bool addFormatAnnotation;

    public string Description { get; private set; }
    public ImageSource Icon { get; private set; }

    public ReadyCodeAction(string desc, ICodeActionEditFactory editFactory, IDocument document, SyntaxNode oldNode, Func<SyntaxNode> newNodeFunc, bool addFormatAnnotation = true, ImageSource icon = null) {
        this.editFactory = editFactory;
        this.document = document;
        this.oldNodes = new[] {oldNode};
        this.newNodeFunc = (e, a) => newNodeFunc();
        this.Description = desc;
        this.Icon = icon;
        this.addFormatAnnotation = addFormatAnnotation;
    }
    public ReadyCodeAction(string desc, ICodeActionEditFactory editFactory, IDocument document, IEnumerable<SyntaxNode> oldNodes, Func<SyntaxNode, SyntaxNode, SyntaxNode> newNodeFunc, bool addFormatAnnotation = true, ImageSource icon = null) {
        this.editFactory = editFactory;
        this.document = document;
        this.oldNodes = oldNodes;
        this.newNodeFunc = newNodeFunc;
        this.Description = desc;
        this.Icon = icon;
        this.addFormatAnnotation = addFormatAnnotation;
    }

    public ICodeActionEdit GetEdit(CancellationToken cancellationToken) {
        var tree = (SyntaxTree)document.GetSyntaxTree();
        var newRoot = tree.Root.ReplaceNodes(
            oldNodes,
            (e, a) => addFormatAnnotation ? CodeActionAnnotations.FormattingAnnotation.AddAnnotationTo(newNodeFunc(e, a)) : newNodeFunc(e, a));
        return editFactory.CreateTreeTransformEdit(document.Project.Solution, tree, newRoot);
    }
}
