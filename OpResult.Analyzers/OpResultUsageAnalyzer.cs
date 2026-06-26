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
        context.RegisterSyntaxNodeAction(AnalyzeIsPatternExpression, SyntaxKind.IsPatternExpression);
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
                out var receiverKey))
        {
            return;
        }

        if (receiverKey.IsValid
            && OpResultSemanticFacts.IsGuardedAccess(
                propertyReference,
                context.Compilation,
                receiverKey,
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
        if (!TryGetOpResultsErrMessageArgument(invocation, context.Compilation, out var messageArgument)
            || !TryGetDirectErrorMessageAccess(
                messageArgument,
                context.Compilation,
                out var errorAccess,
                out var receiverKey)
            || !receiverKey.IsValid
            || !OpResultSemanticFacts.IsGuardedAccess(
                errorAccess,
                context.Compilation,
                receiverKey,
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

    private static void AnalyzeIsPatternExpression(SyntaxNodeAnalysisContext context)
    {
        var patternExpression = (IsPatternExpressionSyntax)context.Node;
        if (!OpResultSemanticFacts.TryGetPseudoBranchPatternTestExpression(
                patternExpression,
                context.SemanticModel,
                context.CancellationToken,
                out var testedExpression))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(
            DiagnosticDescriptors.PseudoBranchTest,
            patternExpression.GetLocation(),
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

    private static bool TryGetOpResultsErrMessageArgument(
        IInvocationOperation invocation,
        Compilation compilation,
        out IOperation messageArgument)
    {
        messageArgument = default!;

        if (!IsOpResultsErrMethod(invocation, compilation))
        {
            return false;
        }

        var messageArgumentOperation = GetArgumentForParameter(invocation, "message");
        if (messageArgumentOperation is null)
        {
            return false;
        }

        if (invocation.Arguments.Length == 1)
        {
            messageArgument = messageArgumentOperation.Value;
            return true;
        }

        var innerErrorArgumentOperation = GetArgumentForParameter(invocation, "innerError");
        if (invocation.Arguments.Length == 2
            && innerErrorArgumentOperation is not null
            && IsNullConstant(innerErrorArgumentOperation.Value))
        {
            messageArgument = messageArgumentOperation.Value;
            return true;
        }

        return false;
    }

    private static IArgumentOperation? GetArgumentForParameter(IInvocationOperation invocation, string parameterName)
    {
        foreach (var argument in invocation.Arguments)
        {
            if (argument.Parameter?.Name == parameterName)
            {
                return argument;
            }
        }

        return null;
    }

    private static bool IsOpResultsErrMethod(IInvocationOperation invocation, Compilation compilation)
    {
        var opResultsType = compilation.GetTypeByMetadataName("OpResult.OpResults");
        var targetMethod = invocation.TargetMethod;
        return opResultsType is not null
            && targetMethod.IsStatic
            && targetMethod.Name == "Err"
            && SymbolEqualityComparer.Default.Equals(targetMethod.ContainingType, opResultsType);
    }

    private static bool IsNullConstant(IOperation operation)
    {
        operation = Unwrap(operation);
        return operation.ConstantValue.HasValue && operation.ConstantValue.Value is null;
    }

    private static bool TryGetDirectErrorMessageAccess(
        IOperation operation,
        Compilation compilation,
        out IPropertyReferenceOperation errorAccess,
        out OpResultReceiverKey receiverKey)
    {
        errorAccess = default!;
        receiverKey = default;

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
                out receiverKey)
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
