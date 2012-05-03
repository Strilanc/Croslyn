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

public static class Util {
    public static IEnumerable<T> Insert<T>(this IEnumerable<T> sequence, int insertPosition, IEnumerable<T> items) {
        return sequence.Take(insertPosition).Concat(items).Concat(sequence.Skip(insertPosition));
    }
    public static ISemanticModel TryGetSemanticModel(this IDocument document) {
        ISemanticModel model;
        return document.TryGetSemanticModel(out model) ? model : null;
    }
    public static T SingleOrDefaultAllowMany<T>(this IEnumerable<T> sequence) {
        var e = sequence.GetEnumerator();
        if (!e.MoveNext()) return default(T);
        var r = e.Current;
        return e.MoveNext() ? default(T) : r;
    }
}
