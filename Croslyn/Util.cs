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
    public static IEnumerable<T> TakeSkipTake<T>(this IEnumerable<T> sequence, int take, int skip) {
        Contract.Requires(take >= 0);
        Contract.Requires(skip >= 0);
        foreach (var e in sequence) {
            if (take > 0) {
                yield return e;
                take -= 1;
            } else if (skip > 0) {
                skip -= 1;
            } else {
                yield return e;
            }
        }
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
