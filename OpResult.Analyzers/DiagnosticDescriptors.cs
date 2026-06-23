using Microsoft.CodeAnalysis;

namespace OpResult.Analyzers;

internal static class DiagnosticDescriptors
{
    public static readonly DiagnosticDescriptor UnguardedValueAccess = new(
        DiagnosticIds.UnguardedValueAccess,
        "Read OpResult value only after proving success",
        "Read 'Value' only after proving the result is Ok",
        "OpResult.Usage",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor UnguardedErrorAccess = new(
        DiagnosticIds.UnguardedErrorAccess,
        "Read OpResult error only after proving failure",
        "Read 'Error' only after proving the result is Err",
        "OpResult.Usage",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor PseudoBranchTest = new(
        DiagnosticIds.PseudoBranchTest,
        "Use IsOk or IsErr to test OpResult branches",
        "Use 'IsOk' or 'IsErr' instead of testing '{0}'",
        "OpResult.Usage",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor UnusedResultReturnValue = new(
        DiagnosticIds.UnusedResultReturnValue,
        "Consume OpResult return values",
        "The returned OpResult value is not consumed",
        "OpResult.Usage",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor DirectErrorChainLoss = new(
        DiagnosticIds.DirectErrorChainLoss,
        "Preserve OpError chains when wrapping failures",
        "Wrap the original OpError instead of rebuilding an error from Error.Message",
        "OpResult.Usage",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);
}
