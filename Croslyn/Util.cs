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
        return TakeSkipPutTake(sequence, take, skip, null);
    }
    public static IEnumerable<T> TakeSkipPutTake<T>(this IEnumerable<T> sequence, int take, int skip, IEnumerable<T> put) {
        Contract.Requires(take >= 0);
        Contract.Requires(skip >= 0);
        using (var en = sequence.GetEnumerator()) {
            while (take > 0) {
                if (en.MoveNext()) 
                    yield return en.Current;
                take -= 1;
            }
            while (skip > 0) {
                en.MoveNext();
                skip -= 1;
            }
            if (put != null) {
                foreach (var e in put) {
                    yield return e;
                }
            }
            while (en.MoveNext()) {
                yield return en.Current;
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
