namespace OpResult.Analyzers;

public static class DiagnosticIds
{
    public const string UnguardedValueAccess = "OPRESULT001";
    public const string UnguardedErrorAccess = "OPRESULT002";
    public const string PseudoBranchTest = "OPRESULT003";
    public const string UnusedResultReturnValue = "OPRESULT004";
    public const string DirectErrorChainLoss = "OPRESULT005";
}
