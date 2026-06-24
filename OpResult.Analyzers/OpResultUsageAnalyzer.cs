using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace OpResult.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class OpResultUsageAnalyzer : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
        ImmutableArray.Create(
            DiagnosticDescriptors.UnguardedValueAccess,
            DiagnosticDescriptors.UnguardedErrorAccess,
            DiagnosticDescriptors.PseudoBranchTest,
            DiagnosticDescriptors.UnusedResultReturnValue,
            DiagnosticDescriptors.DirectErrorChainLoss);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterOperationAction(AnalyzePropertyReference, OperationKind.PropertyReference);
        context.RegisterOperationAction(AnalyzeExpressionStatement, OperationKind.ExpressionStatement);
        context.RegisterOperationAction(AnalyzeInvocation, OperationKind.Invocation);
        context.RegisterSyntaxNodeAction(AnalyzeBinaryExpression, SyntaxKind.EqualsExpression, SyntaxKind.NotEqualsExpression);
    }

    private static void AnalyzePropertyReference(OperationAnalysisContext context)
    {
        var propertyReference = (IPropertyReferenceOperation)context.Operation;
        if (IsInsideNameOf(propertyReference))
        {
            return;
        }

        if (!OpResultSemanticFacts.TryClassifyTrackedProperty(
                propertyReference,
                context.Compilation,
                out var trackedProperty,
                out var receiverSymbol))
        {
            return;
        }

        if (receiverSymbol is not null
            && OpResultSemanticFacts.IsGuardedAccess(
                propertyReference,
                context.Compilation,
                receiverSymbol,
                OpResultSemanticFacts.GetRequiredState(trackedProperty)))
        {
            return;
        }

        var descriptor = trackedProperty switch
        {
            OpResultTrackedProperty.Value => DiagnosticDescriptors.UnguardedValueAccess,
            OpResultTrackedProperty.Error => DiagnosticDescriptors.UnguardedErrorAccess,
            _ => throw new InvalidOperationException($"Unknown tracked property '{trackedProperty}'."),
        };

        context.ReportDiagnostic(Diagnostic.Create(descriptor, propertyReference.Syntax.GetLocation()));
    }

    private static void AnalyzeExpressionStatement(OperationAnalysisContext context)
    {
        var expressionStatement = (IExpressionStatementOperation)context.Operation;
        var expression = expressionStatement.Operation;

        if (expression is ISimpleAssignmentOperation or ICompoundAssignmentOperation)
        {
            return;
        }

        if (!OpResultSemanticFacts.IsOpResultType(expression.Type, context.Compilation))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(
            DiagnosticDescriptors.UnusedResultReturnValue,
            expression.Syntax.GetLocation()));
    }

    private static void AnalyzeInvocation(OperationAnalysisContext context)
    {
        var invocation = (IInvocationOperation)context.Operation;
        if (!IsSingleArgumentOpResultsErr(invocation, context.Compilation)
            || !TryGetDirectErrorMessageAccess(
                invocation.Arguments[0].Value,
                context.Compilation,
                out var errorAccess,
                out var receiverSymbol)
            || receiverSymbol is null
            || !OpResultSemanticFacts.IsGuardedAccess(
                errorAccess,
                context.Compilation,
                receiverSymbol,
                OpResultBranchState.Err))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(
            DiagnosticDescriptors.DirectErrorChainLoss,
            invocation.Syntax.GetLocation()));
    }

    private static void AnalyzeBinaryExpression(SyntaxNodeAnalysisContext context)
    {
        var binaryExpression = (BinaryExpressionSyntax)context.Node;
        if (!OpResultSemanticFacts.TryGetPseudoBranchTestExpression(
                binaryExpression,
                context.SemanticModel,
                context.CancellationToken,
                out var testedExpression))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(
            DiagnosticDescriptors.PseudoBranchTest,
            binaryExpression.GetLocation(),
            testedExpression));
    }

    private static bool IsInsideNameOf(IOperation operation)
    {
        for (var current = operation.Parent; current is not null; current = current.Parent)
        {
            if (current is INameOfOperation)
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsSingleArgumentOpResultsErr(IInvocationOperation invocation, Compilation compilation)
    {
        if (invocation.Arguments.Length != 1)
        {
            return false;
        }

        var opResultsType = compilation.GetTypeByMetadataName("OpResult.OpResults");
        var targetMethod = invocation.TargetMethod;
        return opResultsType is not null
            && targetMethod.IsStatic
            && targetMethod.Name == "Err"
            && SymbolEqualityComparer.Default.Equals(targetMethod.ContainingType, opResultsType);
    }

    private static bool TryGetDirectErrorMessageAccess(
        IOperation operation,
        Compilation compilation,
        out IPropertyReferenceOperation errorAccess,
        out ISymbol? receiverSymbol)
    {
        errorAccess = default!;
        receiverSymbol = default;

        operation = Unwrap(operation);
        if (operation is not IPropertyReferenceOperation messageAccess
            || messageAccess.Instance is null
            || !IsOpErrorMessageProperty(messageAccess.Property, compilation))
        {
            return false;
        }

        var instance = Unwrap(messageAccess.Instance);
        if (instance is not IPropertyReferenceOperation candidateErrorAccess
            || !OpResultSemanticFacts.TryClassifyTrackedProperty(
                candidateErrorAccess,
                compilation,
                out var trackedProperty,
                out receiverSymbol)
            || trackedProperty != OpResultTrackedProperty.Error)
        {
            return false;
        }

        errorAccess = candidateErrorAccess;
        return true;
    }

    private static bool IsOpErrorMessageProperty(IPropertySymbol property, Compilation compilation)
    {
        var opErrorType = compilation.GetTypeByMetadataName("OpResult.OpError");
        return opErrorType is not null
            && property.Name == "Message"
            && SymbolEqualityComparer.Default.Equals(property.ContainingType, opErrorType);
    }

    private static IOperation Unwrap(IOperation operation)
    {
        while (true)
        {
            switch (operation)
            {
                case IConversionOperation conversion:
                    operation = conversion.Operand;
                    continue;
                case IParenthesizedOperation parenthesized:
                    operation = parenthesized.Operand;
                    continue;
                default:
                    return operation;
            }
        }
    }
}
