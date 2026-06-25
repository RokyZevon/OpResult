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

internal readonly struct OpResultReceiverKey : IEquatable<OpResultReceiverKey>
{
    private readonly ImmutableArray<OpResultReceiverSegment> _segments;

    private OpResultReceiverKey(ImmutableArray<OpResultReceiverSegment> segments)
    {
        _segments = segments;
    }

    public bool IsValid => !_segments.IsDefaultOrEmpty;

    public static OpResultReceiverKey Create(string kind, ISymbol? symbol) =>
        new(ImmutableArray.Create(new OpResultReceiverSegment(kind, symbol)));

    public OpResultReceiverKey Append(string kind, ISymbol? symbol)
    {
        if (!IsValid)
        {
            return Create(kind, symbol);
        }

        return new OpResultReceiverKey(_segments.Add(new OpResultReceiverSegment(kind, symbol)));
    }

    public bool StartsWith(OpResultReceiverKey prefix)
    {
        if (!IsValid || !prefix.IsValid || prefix._segments.Length > _segments.Length)
        {
            return false;
        }

        for (var index = 0; index < prefix._segments.Length; index++)
        {
            if (!_segments[index].Equals(prefix._segments[index]))
            {
                return false;
            }
        }

        return true;
    }

    public bool Equals(OpResultReceiverKey other)
    {
        if (IsValid != other.IsValid)
        {
            return false;
        }

        if (!IsValid)
        {
            return true;
        }

        if (_segments.Length != other._segments.Length)
        {
            return false;
        }

        for (var index = 0; index < _segments.Length; index++)
        {
            if (!_segments[index].Equals(other._segments[index]))
            {
                return false;
            }
        }

        return true;
    }

    public override bool Equals(object? obj) =>
        obj is OpResultReceiverKey other && Equals(other);

    public override int GetHashCode()
    {
        if (!IsValid)
        {
            return 0;
        }

        unchecked
        {
            var hashCode = 17;
            foreach (var segment in _segments)
            {
                hashCode = (hashCode * 31) + segment.GetHashCode();
            }

            return hashCode;
        }
    }

    public static bool operator ==(OpResultReceiverKey left, OpResultReceiverKey right) =>
        left.Equals(right);

    public static bool operator !=(OpResultReceiverKey left, OpResultReceiverKey right) =>
        !left.Equals(right);
}

internal readonly struct OpResultReceiverSegment : IEquatable<OpResultReceiverSegment>
{
    private readonly string _kind;
    private readonly ISymbol? _symbol;

    public OpResultReceiverSegment(string kind, ISymbol? symbol)
    {
        _kind = kind;
        _symbol = symbol;
    }

    public bool Equals(OpResultReceiverSegment other) =>
        string.Equals(_kind, other._kind, StringComparison.Ordinal)
        && SymbolEqualityComparer.Default.Equals(_symbol, other._symbol);

    public override bool Equals(object? obj) =>
        obj is OpResultReceiverSegment other && Equals(other);

    public override int GetHashCode()
    {
        unchecked
        {
            var symbolHashCode = _symbol is null
                ? 0
                : SymbolEqualityComparer.Default.GetHashCode(_symbol);
            return (StringComparer.Ordinal.GetHashCode(_kind) * 31) + symbolHashCode;
        }
    }
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
        out OpResultReceiverKey receiverKey)
    {
        trackedProperty = default;
        receiverKey = default;

        if (propertyReference.Instance is null)
        {
            return false;
        }

        var property = propertyReference.Property;
        if (!IsTrackedProperty(property, compilation, out trackedProperty))
        {
            return false;
        }

        TryCreateReceiverKey(propertyReference.Instance, out receiverKey);
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
        OpResultReceiverKey receiverKey,
        OpResultBranchState requiredState)
    {
        return receiverKey.IsValid
            && (IsGuardedByShortCircuitConditionOperand(propertyReference, compilation, receiverKey, requiredState)
                || IsGuardedByEnclosingCondition(propertyReference, compilation, receiverKey, requiredState)
                || IsGuardedByPreviousExitingStatement(propertyReference, compilation, receiverKey, requiredState));
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
            || !TryClassifyErrorMessageAccess(
                candidateExpression,
                semanticModel,
                cancellationToken,
                out var errorAccess,
                out var receiverKey))
        {
            return false;
        }

        if (OpResultSemanticFacts.IsGuardedAccess(
                errorAccess,
                semanticModel.Compilation,
                receiverKey,
                OpResultBranchState.Err))
        {
            return false;
        }

        expressionText = candidateExpression.ToString();
        return true;
    }

    private static bool TryClassifyErrorMessageAccess(
        ExpressionSyntax expression,
        SemanticModel semanticModel,
        CancellationToken cancellationToken,
        out IPropertyReferenceOperation errorAccess,
        out OpResultReceiverKey receiverKey)
    {
        errorAccess = default!;
        receiverKey = default;

        if (expression is not MemberAccessExpressionSyntax { Expression: MemberAccessExpressionSyntax errorAccessSyntax })
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

        if (semanticModel.GetOperation(errorAccessSyntax, cancellationToken) is not IPropertyReferenceOperation errorReference
            || !TryClassifyTrackedProperty(errorReference, semanticModel.Compilation, out var trackedProperty, out receiverKey)
            || trackedProperty != OpResultTrackedProperty.Error)
        {
            return false;
        }

        errorAccess = errorReference;
        return true;
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
        OpResultReceiverKey receiverKey,
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

            if (!TryGetGuardStates(conditional.Condition, compilation, receiverKey, out var whenTrueState, out var whenFalseState))
            {
                continue;
            }

            if (IsDescendantOf(accessOperation, conditional.WhenTrue)
                && whenTrueState == requiredState
                && !HasInvalidatingWriteBeforeAccess(accessOperation, conditional.WhenTrue, receiverKey))
            {
                return true;
            }

            if (conditional.WhenFalse is not null
                && IsDescendantOf(accessOperation, conditional.WhenFalse)
                && whenFalseState == requiredState
                && !HasInvalidatingWriteBeforeAccess(accessOperation, conditional.WhenFalse, receiverKey))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsGuardedByShortCircuitConditionOperand(
        IOperation accessOperation,
        Compilation compilation,
        OpResultReceiverKey receiverKey,
        OpResultBranchState requiredState)
    {
        for (var current = accessOperation.Parent; current is not null; current = current.Parent)
        {
            if (current is IAnonymousFunctionOperation or ILocalFunctionOperation)
            {
                return false;
            }

            if (current is not IBinaryOperation binary
                || !IsDescendantOf(accessOperation, binary.RightOperand)
                || !TryGetGuardStates(binary.LeftOperand, compilation, receiverKey, out var leftTrueState, out var leftFalseState))
            {
                continue;
            }

            if (binary.OperatorKind == BinaryOperatorKind.ConditionalAnd
                && leftTrueState == requiredState
                && !HasInvalidatingWriteBeforeAccess(accessOperation, binary.RightOperand, receiverKey))
            {
                return true;
            }

            if (binary.OperatorKind == BinaryOperatorKind.ConditionalOr
                && leftFalseState == requiredState
                && !HasInvalidatingWriteBeforeAccess(accessOperation, binary.RightOperand, receiverKey))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsGuardedByPreviousExitingStatement(
        IOperation accessOperation,
        Compilation compilation,
        OpResultReceiverKey receiverKey,
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

        for (var index = statementIndex - 1; index >= 0; index--)
        {
            if (statements[index] is IConditionalOperation conditional
                && TryGetGuardStates(conditional.Condition, compilation, receiverKey, out var whenTrueState, out var whenFalseState))
            {
                if (DoesOperationExitBeforeAccess(conditional.WhenTrue, accessOperation)
                    && whenFalseState == requiredState
                    && !HasReachingInvalidatingWrite(conditional.WhenFalse, accessOperation, receiverKey))
                {
                    return true;
                }

                if (conditional.WhenFalse is not null
                    && DoesOperationExitBeforeAccess(conditional.WhenFalse, accessOperation)
                    && whenTrueState == requiredState
                    && !HasReachingInvalidatingWrite(conditional.WhenTrue, accessOperation, receiverKey))
                {
                    return true;
                }
            }

            if (HasReachingInvalidatingWrite(statements[index], accessOperation, receiverKey))
            {
                return false;
            }
        }

        return false;
    }

    private static bool TryGetGuardStates(
        IOperation condition,
        Compilation compilation,
        OpResultReceiverKey receiverKey,
        out OpResultBranchState? whenTrueState,
        out OpResultBranchState? whenFalseState)
    {
        whenTrueState = null;
        whenFalseState = null;

        condition = Unwrap(condition);

        if (condition is IUnaryOperation { OperatorKind: UnaryOperatorKind.Not } unary)
        {
            if (!TryGetGuardStates(unary.Operand, compilation, receiverKey, out var operandTrueState, out var operandFalseState))
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
                receiverKey,
                out whenTrueState,
                out whenFalseState))
        {
            return true;
        }

        if (condition is not IPropertyReferenceOperation propertyReference
            || propertyReference.Instance is null
            || !TryCreateReceiverKey(propertyReference.Instance, out var candidateReceiverKey)
            || candidateReceiverKey != receiverKey
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
        OpResultReceiverKey receiverKey,
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
            receiverKey,
            out var leftTrueState,
            out var leftFalseState);
        var hasRightGuard = TryGetGuardStates(
            binary.RightOperand,
            compilation,
            receiverKey,
            out var rightTrueState,
            out var rightFalseState);

        if (!hasLeftGuard && !hasRightGuard)
        {
            return false;
        }

        var rightHasInvalidatingWrite = ContainsInvalidatingWrite(binary.RightOperand, receiverKey);

        if (IsBooleanConjunction(binary.OperatorKind))
        {
            var leftProofState = hasLeftGuard && !rightHasInvalidatingWrite
                ? MergeCompatibleStates(leftTrueState, rightTrueState)
                : null;
            var rightProofState = hasRightGuard
                ? rightTrueState
                : null;

            whenTrueState = MergeCompatibleStates(leftProofState, rightProofState);
            whenFalseState = hasLeftGuard && hasRightGuard
                ? MergeCompatibleStates(leftFalseState, rightFalseState)
                : null;
        }
        else
        {
            whenTrueState = hasLeftGuard && hasRightGuard
                ? MergeCompatibleStates(leftTrueState, rightTrueState)
                : null;
            var leftProofState = hasLeftGuard && !rightHasInvalidatingWrite
                ? leftFalseState
                : null;
            var rightProofState = hasRightGuard
                ? rightFalseState
                : null;

            whenFalseState = MergeCompatibleStates(leftProofState, rightProofState);
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

    private static bool DoesOperationExitBeforeAccess(IOperation operation, IOperation accessOperation)
    {
        operation = Unwrap(operation);

        if (operation is IBlockOperation block)
        {
            if (block.Operations.Length == 0)
            {
                return false;
            }

            return DoesOperationExitBeforeAccess(block.Operations[block.Operations.Length - 1], accessOperation);
        }

        if (operation is IConditionalOperation conditional)
        {
            return conditional.WhenFalse is not null
                && DoesOperationExitBeforeAccess(conditional.WhenTrue, accessOperation)
                && DoesOperationExitBeforeAccess(conditional.WhenFalse, accessOperation);
        }

        return operation is IReturnOperation
            || operation is IThrowOperation
            || (operation is IBranchOperation branch
                && branch.BranchKind == BranchKind.Continue
                && DoesContinueSkipAccess(branch, accessOperation));
    }

    private static bool HasInvalidatingWriteBeforeAccess(
        IOperation accessOperation,
        IOperation guardedBranch,
        OpResultReceiverKey receiverKey)
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

            if (IsInvalidatingWrite(operation, receiverKey)
                && !IsInsidePathThatExitsBeforeAccess(operation, accessOperation))
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasReachingInvalidatingWrite(
        IOperation? operation,
        IOperation accessOperation,
        OpResultReceiverKey receiverKey)
    {
        if (operation is null)
        {
            return false;
        }

        var accessFunctionBoundary = GetContainingFunctionBoundary(accessOperation);

        foreach (var descendant in EnumerateOperations(operation))
        {
            if (!HasSameFunctionBoundary(descendant, accessFunctionBoundary))
            {
                continue;
            }

            if (IsInvalidatingWrite(descendant, receiverKey)
                && !IsInsidePathThatExitsBeforeAccess(descendant, accessOperation))
            {
                return true;
            }
        }

        return false;
    }

    private static bool ContainsInvalidatingWrite(IOperation operation, OpResultReceiverKey receiverKey)
    {
        foreach (var descendant in EnumerateOperations(operation))
        {
            if (IsInvalidatingWrite(descendant, receiverKey))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsInvalidatingWrite(IOperation operation, OpResultReceiverKey receiverKey) =>
        operation switch
        {
            ISimpleAssignmentOperation assignment =>
                ReferencesReceiver(assignment.Target, receiverKey),
            IDeconstructionAssignmentOperation deconstruction =>
                ReferencesReceiver(deconstruction.Target, receiverKey),
            IArgumentOperation argument when IsWriteArgument(argument) =>
                ReferencesReceiver(argument.Value, receiverKey),
            _ => false,
        };

    private static bool IsWriteArgument(IArgumentOperation argument) =>
        argument.Parameter?.RefKind is RefKind.Ref or RefKind.Out;

    private static bool ReferencesReceiver(IOperation operation, OpResultReceiverKey receiverKey)
    {
        operation = Unwrap(operation);

        if (TryCreateReceiverKey(operation, out var candidateReceiverKey))
        {
            return AreRelatedReceiverKeys(candidateReceiverKey, receiverKey);
        }

        foreach (var child in operation.ChildOperations)
        {
            if (ReferencesReceiver(child, receiverKey))
            {
                return true;
            }
        }

        return false;
    }

    private static bool AreRelatedReceiverKeys(OpResultReceiverKey candidateReceiverKey, OpResultReceiverKey receiverKey) =>
        candidateReceiverKey == receiverKey
        || receiverKey.StartsWith(candidateReceiverKey)
        || candidateReceiverKey.StartsWith(receiverKey);

    private static bool IsInsidePathThatExitsBeforeAccess(IOperation operation, IOperation accessOperation)
    {
        for (var current = operation; current is not null; current = current.Parent)
        {
            if (current.Parent is not IBlockOperation block || IsDescendantOf(accessOperation, block))
            {
                continue;
            }

            var statementIndex = IndexOf(block.Operations, current);
            if (statementIndex >= 0 && DoesStatementSuffixExitBeforeAccess(block, statementIndex, accessOperation))
            {
                return true;
            }
        }

        return false;
    }

    private static bool DoesStatementSuffixExitBeforeAccess(
        IBlockOperation block,
        int startIndex,
        IOperation accessOperation)
    {
        for (var index = block.Operations.Length - 1; index >= startIndex; index--)
        {
            if (DoesOperationExitBeforeAccess(block.Operations[index], accessOperation))
            {
                return true;
            }
        }

        return false;
    }

    private static bool DoesContinueSkipAccess(IBranchOperation branch, IOperation accessOperation)
    {
        var loopSyntax = GetContainingLoopSyntax(branch.Syntax);
        return loopSyntax is not null
            && loopSyntax.Span.Contains(accessOperation.Syntax.SpanStart)
            && branch.Syntax.SpanStart < accessOperation.Syntax.SpanStart;
    }

    private static SyntaxNode? GetContainingLoopSyntax(SyntaxNode syntax)
    {
        for (var current = syntax.Parent; current is not null; current = current.Parent)
        {
            if (current is ForStatementSyntax
                or ForEachStatementSyntax
                or ForEachVariableStatementSyntax
                or WhileStatementSyntax
                or DoStatementSyntax)
            {
                return current;
            }
        }

        return null;
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

    private static bool TryCreateReceiverKey(IOperation operation, out OpResultReceiverKey receiverKey)
    {
        receiverKey = default;
        operation = Unwrap(operation);

        switch (operation)
        {
            case ILocalReferenceOperation localReference:
                receiverKey = OpResultReceiverKey.Create("local", localReference.Local);
                return true;

            case IParameterReferenceOperation parameterReference:
                receiverKey = OpResultReceiverKey.Create("parameter", parameterReference.Parameter);
                return true;

            case IInstanceReferenceOperation:
                receiverKey = OpResultReceiverKey.Create("instance", null);
                return true;

            case IFieldReferenceOperation fieldReference:
                return TryCreateMemberReceiverKey(fieldReference.Instance, fieldReference.Field, "field", out receiverKey);

            case IPropertyReferenceOperation propertyReference:
                return TryCreateMemberReceiverKey(propertyReference.Instance, propertyReference.Property, "property", out receiverKey);

            default:
                return false;
        }
    }

    private static bool TryCreateMemberReceiverKey(
        IOperation? instance,
        ISymbol member,
        string kind,
        out OpResultReceiverKey receiverKey)
    {
        if (instance is null)
        {
            receiverKey = OpResultReceiverKey.Create(kind, member);
            return true;
        }

        if (!TryCreateReceiverKey(instance, out var instanceKey))
        {
            receiverKey = default;
            return false;
        }

        receiverKey = instanceKey.Append(kind, member);
        return true;
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
