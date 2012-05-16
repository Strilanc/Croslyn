using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Roslyn.Compilers.CSharp;
using System.Diagnostics.Contracts;
using Roslyn.Services;
using Roslyn.Compilers.Common;
using Strilbrary.Collections;
using Roslyn.Compilers;

public struct TentativeBool : IEquatable<TentativeBool>, IComparable<TentativeBool> {
    public static readonly TentativeBool False = new TentativeBool(-2);
    public static readonly TentativeBool ProbablyFalse = new TentativeBool(-1);
    public static readonly TentativeBool Unknown = new TentativeBool(0);
    public static readonly TentativeBool ProbablyTrue = new TentativeBool(1);
    public static readonly TentativeBool True = new TentativeBool(2);

    private readonly int _value;
    private TentativeBool(int value) { this._value = value; }

    public bool IsDefinitelyFalse { get { return _value <= False._value; } }
    public bool IsProbablyFalse { get { return _value <= ProbablyFalse._value; } }
    public bool IsProbablyTrue { get { return _value >= ProbablyTrue._value; } }
    public bool IsDefinitelyTrue { get { return _value >= True._value; } }
    public bool IsUnknown { get { return _value == Unknown._value; } }

    public bool? ProbableResult {
        get {
            if (IsProbablyTrue) return true;
            if (IsProbablyFalse) return false;
            return null;
        }
    }
    public TentativeBool Max(TentativeBool other) {
        return new TentativeBool(Math.Max(this._value, other._value));
    }
    public TentativeBool Min(TentativeBool other) {
        return new TentativeBool(Math.Min(this._value, other._value));
    }
    public TentativeBool Inverse { get { return new TentativeBool(-_value); } }

    public static implicit operator TentativeBool(bool value) {
        return value ? True : False;
    }
    public static implicit operator TentativeBool(bool? value) {
        return value.HasValue ? value.Value : Unknown;
    }

    public static bool operator ==(TentativeBool v1, TentativeBool v2) {
        return v1.Equals(v2);
    }
    public static bool operator !=(TentativeBool v1, TentativeBool v2) {
        return !v1.Equals(v2);
    }

    public int CompareTo(TentativeBool other) {
        return this._value - other._value;
    }
    public override bool Equals(object obj) {
        return obj is TentativeBool && this.Equals((TentativeBool)obj);
    }
    public override int GetHashCode() {
        return _value.GetHashCode();
    }
    public bool Equals(TentativeBool other) {
        return this._value == other._value;
    }
    public override string ToString() {
        if (this == False) return "False";
        if (this == ProbablyFalse) return "ProbablyFalse";
        if (this == Unknown) return "Unknown";
        if (this == ProbablyTrue) return "ProbablyTrue";
        if (this == True) return "True";
        return "?";
    }
}
