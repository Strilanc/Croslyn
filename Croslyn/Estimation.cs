using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Roslyn.Compilers.CSharp;
using System.Diagnostics.Contracts;
using Roslyn.Services;
using Roslyn.Compilers.Common;
using Strilbrary.Collections;

public static class Estimation {
    public static double Bloat(this SyntaxNode node) {
        return node.DescendantNodesAndSelf().Count();
    }
    public static double Depth(this SyntaxNode node) {
        var b = node.AncestorsAndSelf().Count();
        var d = 0;
        var q = new Queue<Tuple<SyntaxNode, int>>();
        q.Enqueue(Tuple.Create(node, 0));
        while (q.Count > 0) {
            var e = q.Dequeue();
            foreach (var c in e.Item1.ChildNodes())
                q.Enqueue(Tuple.Create(c, e.Item2 + 1));
            if (e.Item2 > d) d = e.Item2;
        }
        return b + d;
    }
}
