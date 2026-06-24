using System.Collections.Immutable;
using System.Collections.Generic;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;

namespace OpResult.Analyzers;

internal enum OpResultBranchState
{
    Ok,
    Err,
}

internal enum OpResultTrackedProperty
{
    Value,
    Error,
}

internal static class OpResultSemanticFacts
{
    public static bool IsOpResultType(ITypeSymbol? type, Compilation compilation)
    {
        if (type is not INamedTypeSymbol namedType)
        {
            return false;
        }

        return IsOpResultLikeType(namedType, compilation);
    }

    public static bool TryClassifyTrackedProperty(
        IPropertyReferenceOperation propertyReference,
        Compilation compilation,
        out OpResultTrackedProperty trackedProperty,
        out ISymbol? receiverSymbol)
    {
        trackedProperty = default;
        receiverSymbol = default;

        if (propertyReference.Instance is null)
        {
            return false;
        }

        var property = propertyReference.Property;
        if (!IsTrackedProperty(property, compilation, out trackedProperty))
        {
            return false;
        }

        receiverSymbol = GetReferencedSymbol(propertyReference.Instance);
        return true;
    }

    public static OpResultBranchState GetRequiredState(OpResultTrackedProperty trackedProperty) =>
        trackedProperty switch
        {
            OpResultTrackedProperty.Value => OpResultBranchState.Ok,
            OpResultTrackedProperty.Error => OpResultBranchState.Err,
            _ => throw new InvalidOperationException($"Unknown tracked property '{trackedProperty}'."),
        };

    public static bool TryGetPseudoBranchTestExpression(
        BinaryExpressionSyntax binaryExpression,
        SemanticModel semanticModel,
        CancellationToken cancellationToken,
        out string expressionText)
    {
        expressionText = string.Empty;

        if (TryGetTrackedPropertyComparedToNull(
                binaryExpression.Left,
                binaryExpression.Right,
                semanticModel,
                cancellationToken,
                out expressionText)
            || TryGetTrackedPropertyComparedToNull(
                binaryExpression.Right,
                binaryExpression.Left,
                semanticModel,
                cancellationToken,
                out expressionText))
        {
            return true;
        }

        if (binaryExpression.IsKind(SyntaxKind.EqualsExpression)
            && (TryGetErrorMessageComparedToEmptyString(
                    binaryExpression.Left,
                    binaryExpression.Right,
                    semanticModel,
                    cancellationToken,
                    out expressionText)
                || TryGetErrorMessageComparedToEmptyString(
                    binaryExpression.Right,
                    binaryExpression.Left,
                    semanticModel,
                    cancellationToken,
                    out expressionText)))
        {
            return true;
        }

        return false;
    }

    public static bool IsGuardedAccess(
        IPropertyReferenceOperation propertyReference,
        Compilation compilation,
        ISymbol receiverSymbol,
        OpResultBranchState requiredState)
    {
        return IsGuardedByEnclosingCondition(propertyReference, compilation, receiverSymbol, requiredState)
            || IsGuardedByImmediatePreviousStatement(propertyReference, compilation, receiverSymbol, requiredState);
    }

    private static bool IsTrackedProperty(
        IPropertySymbol property,
        Compilation compilation,
        out OpResultTrackedProperty trackedProperty)
    {
        trackedProperty = default;
        var containingType = property.ContainingType;
        if (containingType is null)
        {
            return false;
        }

        if (property.Name == "Value" && IsGenericOpResultType(containingType, compilation))
        {
            trackedProperty = OpResultTrackedProperty.Value;
            return true;
        }

        if (property.Name == "Error" && IsOpResultLikeType(containingType, compilation))
        {
            trackedProperty = OpResultTrackedProperty.Error;
            return true;
        }

        return false;
    }

    private static bool TryClassifyTrackedPropertyAccess(
        ExpressionSyntax expression,
        SemanticModel semanticModel,
        CancellationToken cancellationToken,
        out OpResultTrackedProperty trackedProperty)
    {
        trackedProperty = default;

        var property = semanticModel.GetSymbolInfo(expression, cancellationToken).Symbol as IPropertySymbol;
        if (property is null)
        {
            return false;
        }

        return IsTrackedProperty(property, semanticModel.Compilation, out trackedProperty);
    }

    private static bool TryGetTrackedPropertyComparedToNull(
        ExpressionSyntax candidateExpression,
        ExpressionSyntax nullExpression,
        SemanticModel semanticModel,
        CancellationToken cancellationToken,
        out string expressionText)
    {
        expressionText = string.Empty;

        if (!IsNullLiteral(nullExpression)
            || !TryClassifyTrackedPropertyAccess(candidateExpression, semanticModel, cancellationToken, out _))
        {
            return false;
        }

        expressionText = candidateExpression.ToString();
        return true;
    }

    private static bool TryGetErrorMessageComparedToEmptyString(
        ExpressionSyntax candidateExpression,
        ExpressionSyntax emptyStringExpression,
        SemanticModel semanticModel,
        CancellationToken cancellationToken,
        out string expressionText)
    {
        expressionText = string.Empty;

        if (!IsEmptyStringExpression(emptyStringExpression, semanticModel, cancellationToken)
            || !TryClassifyErrorMessageAccess(candidateExpression, semanticModel, cancellationToken))
        {
            return false;
        }

        expressionText = candidateExpression.ToString();
        return true;
    }

    private static bool TryClassifyErrorMessageAccess(
        ExpressionSyntax expression,
        SemanticModel semanticModel,
        CancellationToken cancellationToken)
    {
        if (expression is not MemberAccessExpressionSyntax { Expression: MemberAccessExpressionSyntax errorAccess })
        {
            return false;
        }

        var property = semanticModel.GetSymbolInfo(expression, cancellationToken).Symbol as IPropertySymbol;
        if (property is null
            || property.Name != "Message"
            || !IsOpErrorType(property.ContainingType, semanticModel.Compilation))
        {
            return false;
        }

        return TryClassifyTrackedPropertyAccess(errorAccess, semanticModel, cancellationToken, out var trackedProperty)
            && trackedProperty == OpResultTrackedProperty.Error;
    }

    private static bool IsNullLiteral(ExpressionSyntax expression) =>
        expression.IsKind(SyntaxKind.NullLiteralExpression);

    private static bool IsEmptyStringExpression(
        ExpressionSyntax expression,
        SemanticModel semanticModel,
        CancellationToken cancellationToken)
    {
        if (expression is LiteralExpressionSyntax literal
            && literal.IsKind(SyntaxKind.StringLiteralExpression)
            && string.Equals(literal.Token.ValueText, string.Empty, StringComparison.Ordinal))
        {
            return true;
        }

        if (expression is not MemberAccessExpressionSyntax
            {
                Expression: PredefinedTypeSyntax predefinedType,
                Name.Identifier.ValueText: "Empty",
            }
            || !predefinedType.Keyword.IsKind(SyntaxKind.StringKeyword))
        {
            return false;
        }

        var field = semanticModel.GetSymbolInfo(expression, cancellationToken).Symbol as IFieldSymbol;
        return field is not null
            && field.Name == "Empty"
            && field.ContainingType.SpecialType == SpecialType.System_String;
    }

    private static bool IsGuardedByEnclosingCondition(
        IOperation accessOperation,
        Compilation compilation,
        ISymbol receiverSymbol,
        OpResultBranchState requiredState)
    {
        for (var current = accessOperation.Parent; current is not null; current = current.Parent)
        {
            if (current is IAnonymousFunctionOperation or ILocalFunctionOperation)
            {
                return false;
            }

            if (current is not IConditionalOperation conditional)
            {
                continue;
            }

            if (!TryGetGuardStates(conditional.Condition, compilation, receiverSymbol, out var whenTrueState, out var whenFalseState))
            {
                continue;
            }

            if (IsDescendantOf(accessOperation, conditional.WhenTrue)
                && whenTrueState == requiredState
                && !HasInvalidatingAssignmentBeforeAccess(accessOperation, conditional.WhenTrue, receiverSymbol))
            {
                return true;
            }

            if (conditional.WhenFalse is not null
                && IsDescendantOf(accessOperation, conditional.WhenFalse)
                && whenFalseState == requiredState
                && !HasInvalidatingAssignmentBeforeAccess(accessOperation, conditional.WhenFalse, receiverSymbol))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsGuardedByImmediatePreviousStatement(
        IOperation accessOperation,
        Compilation compilation,
        ISymbol receiverSymbol,
        OpResultBranchState requiredState)
    {
        var statement = GetContainingStatement(accessOperation);
        if (statement is null || statement.Parent is not IBlockOperation block)
        {
            return false;
        }

        var statements = block.Operations;
        var statementIndex = IndexOf(statements, statement);
        if (statementIndex <= 0)
        {
            return false;
        }

        if (statements[statementIndex - 1] is not IConditionalOperation conditional
            || conditional.WhenFalse is not null
            || !TryGetGuardStates(conditional.Condition, compilation, receiverSymbol, out var whenTrueState, out var whenFalseState))
        {
            return false;
        }

        return DoesOperationExit(conditional.WhenTrue) && whenFalseState == requiredState;
    }

    private static bool TryGetGuardStates(
        IOperation condition,
        Compilation compilation,
        ISymbol receiverSymbol,
        out OpResultBranchState? whenTrueState,
        out OpResultBranchState? whenFalseState)
    {
        whenTrueState = null;
        whenFalseState = null;

        condition = Unwrap(condition);

        if (condition is IUnaryOperation { OperatorKind: UnaryOperatorKind.Not } unary)
        {
            if (!TryGetGuardStates(unary.Operand, compilation, receiverSymbol, out var operandTrueState, out var operandFalseState))
            {
                return false;
            }

            whenTrueState = operandFalseState;
            whenFalseState = operandTrueState;
            return true;
        }

        if (condition is IBinaryOperation binary
            && TryGetBinaryGuardStates(
                binary,
                compilation,
                receiverSymbol,
                out whenTrueState,
                out whenFalseState))
        {
            return true;
        }

        if (condition is not IPropertyReferenceOperation propertyReference
            || propertyReference.Instance is null
            || !SymbolEqualityComparer.Default.Equals(GetReferencedSymbol(propertyReference.Instance), receiverSymbol)
            || !IsOpResultLikeType(propertyReference.Property.ContainingType, compilation))
        {
            return false;
        }

        switch (propertyReference.Property.Name)
        {
            case "IsOk":
                whenTrueState = OpResultBranchState.Ok;
                whenFalseState = OpResultBranchState.Err;
                return true;

            case "IsErr":
                whenTrueState = OpResultBranchState.Err;
                whenFalseState = OpResultBranchState.Ok;
                return true;

            default:
                return false;
        }
    }

    private static bool TryGetBinaryGuardStates(
        IBinaryOperation binary,
        Compilation compilation,
        ISymbol receiverSymbol,
        out OpResultBranchState? whenTrueState,
        out OpResultBranchState? whenFalseState)
    {
        whenTrueState = null;
        whenFalseState = null;

        if (!IsBooleanConjunction(binary.OperatorKind) && !IsBooleanDisjunction(binary.OperatorKind))
        {
            return false;
        }

        var hasLeftGuard = TryGetGuardStates(
            binary.LeftOperand,
            compilation,
            receiverSymbol,
            out var leftTrueState,
            out var leftFalseState);
        var hasRightGuard = TryGetGuardStates(
            binary.RightOperand,
            compilation,
            receiverSymbol,
            out var rightTrueState,
            out var rightFalseState);

        if (!hasLeftGuard && !hasRightGuard)
        {
            return false;
        }

        if (IsBooleanConjunction(binary.OperatorKind))
        {
            whenTrueState = MergeCompatibleStates(leftTrueState, rightTrueState);
            whenFalseState = hasLeftGuard && hasRightGuard
                ? MergeCompatibleStates(leftFalseState, rightFalseState)
                : null;
        }
        else
        {
            whenTrueState = hasLeftGuard && hasRightGuard
                ? MergeCompatibleStates(leftTrueState, rightTrueState)
                : null;
            whenFalseState = MergeCompatibleStates(leftFalseState, rightFalseState);
        }

        return whenTrueState is not null || whenFalseState is not null;
    }

    private static bool IsBooleanConjunction(BinaryOperatorKind operatorKind) =>
        operatorKind is BinaryOperatorKind.ConditionalAnd or BinaryOperatorKind.And;

    private static bool IsBooleanDisjunction(BinaryOperatorKind operatorKind) =>
        operatorKind is BinaryOperatorKind.ConditionalOr or BinaryOperatorKind.Or;

    private static OpResultBranchState? MergeCompatibleStates(
        OpResultBranchState? leftState,
        OpResultBranchState? rightState)
    {
        if (leftState is null)
        {
            return rightState;
        }

        if (rightState is null || leftState == rightState)
        {
            return leftState;
        }

        return null;
    }

    private static bool DoesOperationExit(IOperation operation)
    {
        operation = Unwrap(operation);

        if (operation is IBlockOperation block)
        {
            if (block.Operations.Length == 0)
            {
                return false;
            }

            return DoesOperationExit(block.Operations[block.Operations.Length - 1]);
        }

        return operation is IReturnOperation or IThrowOperation;
    }

    private static bool HasInvalidatingAssignmentBeforeAccess(
        IOperation accessOperation,
        IOperation guardedBranch,
        ISymbol receiverSymbol)
    {
        var accessStart = accessOperation.Syntax.SpanStart;
        var accessFunctionBoundary = GetContainingFunctionBoundary(accessOperation);

        foreach (var operation in EnumerateOperations(guardedBranch))
        {
            if (operation.Syntax.SpanStart >= accessStart
                || !HasSameFunctionBoundary(operation, accessFunctionBoundary))
            {
                continue;
            }

            if (IsInvalidatingWrite(operation, receiverSymbol))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsInvalidatingWrite(IOperation operation, ISymbol receiverSymbol) =>
        operation switch
        {
            ISimpleAssignmentOperation assignment =>
                ReferencesSymbol(assignment.Target, receiverSymbol),
            IDeconstructionAssignmentOperation deconstruction =>
                ReferencesSymbol(deconstruction.Target, receiverSymbol),
            IArgumentOperation argument when IsWriteArgument(argument) =>
                ReferencesSymbol(argument.Value, receiverSymbol),
            _ => false,
        };

    private static bool IsWriteArgument(IArgumentOperation argument) =>
        argument.Parameter?.RefKind is RefKind.Ref or RefKind.Out;

    private static bool ReferencesSymbol(IOperation operation, ISymbol receiverSymbol)
    {
        operation = Unwrap(operation);

        if (SymbolEqualityComparer.Default.Equals(GetReferencedSymbol(operation), receiverSymbol))
        {
            return true;
        }

        foreach (var child in operation.ChildOperations)
        {
            if (ReferencesSymbol(child, receiverSymbol))
            {
                return true;
            }
        }

        return false;
    }

    private static IOperation? GetContainingStatement(IOperation operation)
    {
        for (var current = operation; current is not null; current = current.Parent)
        {
            if (current is IOperation { Parent: IBlockOperation })
            {
                return current;
            }
        }

        return null;
    }

    private static int IndexOf(ImmutableArray<IOperation> operations, IOperation target)
    {
        for (var index = 0; index < operations.Length; index++)
        {
            if (ReferenceEquals(operations[index], target))
            {
                return index;
            }
        }

        return -1;
    }

    private static IOperation? GetContainingFunctionBoundary(IOperation operation)
    {
        for (var current = operation.Parent; current is not null; current = current.Parent)
        {
            if (current is IAnonymousFunctionOperation or ILocalFunctionOperation)
            {
                return current;
            }
        }

        return null;
    }

    private static bool HasSameFunctionBoundary(IOperation operation, IOperation? expectedBoundary)
    {
        return ReferenceEquals(GetContainingFunctionBoundary(operation), expectedBoundary);
    }

    private static IEnumerable<IOperation> EnumerateOperations(IOperation root)
    {
        yield return root;

        foreach (var child in root.ChildOperations)
        {
            foreach (var descendant in EnumerateOperations(child))
            {
                yield return descendant;
            }
        }
    }

    private static bool IsDescendantOf(IOperation operation, IOperation? ancestor)
    {
        if (ancestor is null)
        {
            return false;
        }

        for (var current = operation; current is not null; current = current.Parent)
        {
            if (ReferenceEquals(current, ancestor))
            {
                return true;
            }
        }

        return false;
    }

    private static ISymbol? GetReferencedSymbol(IOperation operation)
    {
        operation = Unwrap(operation);

        return operation switch
        {
            ILocalReferenceOperation localReference => localReference.Local,
            IParameterReferenceOperation parameterReference => parameterReference.Parameter,
            IFieldReferenceOperation fieldReference => fieldReference.Field,
            IPropertyReferenceOperation propertyReference => propertyReference.Property,
            _ => null,
        };
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

    private static bool IsOpResultLikeType(INamedTypeSymbol type, Compilation compilation) =>
        IsNonGenericOpResultType(type, compilation) || IsGenericOpResultType(type, compilation);

    private static bool IsNonGenericOpResultType(INamedTypeSymbol type, Compilation compilation)
    {
        var opResultType = compilation.GetTypeByMetadataName("OpResult.OpResult");
        return opResultType is not null
            && SymbolEqualityComparer.Default.Equals(type.OriginalDefinition, opResultType);
    }

    private static bool IsGenericOpResultType(INamedTypeSymbol type, Compilation compilation)
    {
        var opResultType = compilation.GetTypeByMetadataName("OpResult.OpResult`1");
        return opResultType is not null
            && SymbolEqualityComparer.Default.Equals(type.OriginalDefinition, opResultType);
    }

    private static bool IsOpErrorType(INamedTypeSymbol type, Compilation compilation)
    {
        var opErrorType = compilation.GetTypeByMetadataName("OpResult.OpError");
        return opErrorType is not null
            && SymbolEqualityComparer.Default.Equals(type.OriginalDefinition, opErrorType);
    }
}
