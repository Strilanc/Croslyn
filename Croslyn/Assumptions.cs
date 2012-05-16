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

public struct Assumptions {
    public static readonly Assumptions All = new Assumptions(true, true, true);
    public static readonly Assumptions None = default(Assumptions);

    public readonly bool PropertyGettersHaveNoSideEffects;
    public readonly bool OperatorsHaveNoSideEffects;
    public readonly bool IterationHasNoSideEffects;
    public Assumptions(bool propertyGettersHaveNoSideEffects, bool operatorsHaveNoSideEffects, bool iterationHasNoSideEffects) {
        this.PropertyGettersHaveNoSideEffects = propertyGettersHaveNoSideEffects;
        this.OperatorsHaveNoSideEffects = operatorsHaveNoSideEffects;
        this.IterationHasNoSideEffects = iterationHasNoSideEffects;
    }
}
