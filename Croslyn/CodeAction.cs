using System.Linq;
using System.Threading;
using System.Windows.Media;
using Roslyn.Compilers.CSharp;
using Roslyn.Services;
using Roslyn.Services.Editor;
using System;

internal class ReadyCodeAction : ICodeAction {
    private readonly ICodeActionEditFactory editFactory;
    private readonly IDocument document;
    private readonly SyntaxNode oldNode;
    private readonly Func<SyntaxNode> newNodeFunc;
    private readonly bool addFormatAnnotation;

    public string Description { get; private set; }
    public ImageSource Icon { get; private set; }

    public ReadyCodeAction(string desc, ICodeActionEditFactory editFactory, IDocument document, SyntaxNode oldNode, Func<SyntaxNode> newNodeFunc, bool addFormatAnnotation = true, ImageSource icon = null) {
        this.editFactory = editFactory;
        this.document = document;
        this.oldNode = oldNode;
        this.newNodeFunc = newNodeFunc;
        this.Description = desc;
        this.Icon = icon;
        this.addFormatAnnotation = addFormatAnnotation;
    }

    public ICodeActionEdit GetEdit(CancellationToken cancellationToken) {
        var newNode = newNodeFunc();
        var f = addFormatAnnotation ? CodeActionAnnotations.FormattingAnnotation.AddAnnotationTo(newNode) : newNode;
        var tree = (SyntaxTree)document.GetSyntaxTree();
        var newRoot = tree.Root.ReplaceNode(oldNode, f);
        return editFactory.CreateTreeTransformEdit(document.Project.Solution, tree, newRoot);
    }
}
