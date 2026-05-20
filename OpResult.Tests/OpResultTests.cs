namespace OpResult.Tests;

using System.Diagnostics.CodeAnalysis;
using System.Reflection;

public class OpResultTests
{
    [Fact]
    public void NonGenericOpResult_IsPublicAndSupportsVoidLikeOkErr()
    {
        var nonGenericType = GetNonGenericOpResultType();
        Assert.True(nonGenericType.IsPublic);
        Assert.True(nonGenericType.IsValueType);

        var okFactory = FindOpResultsFactoryMethod(
            nameof(OpResults.Ok),
            parameterCount: 0,
            genericArity: 0,
            method => method.ReturnType == nonGenericType);

        var errFactory = FindOpResultsFactoryMethod(
            nameof(OpResults.Err),
            parameterCount: 1,
            genericArity: 0,
            method => method.ReturnType == nonGenericType);

        var ok = okFactory.Invoke(null, null)!;
        Assert.True(ReadIsOk(ok));
        Assert.False(ReadIsErr(ok));

        var err = errFactory.Invoke(null, new object?[] { "write failed" })!;
        Assert.False(ReadIsOk(err));
        Assert.True(ReadIsErr(err));
        Assert.Equal("write failed", ReadErrorMessage(err));
    }

    [Fact]
    public void OpResults_FactorySurfaceMatchesSpec()
    {
        var nonGenericType = GetNonGenericOpResultType();

        var okWithoutValue = FindOpResultsFactoryMethod(
            nameof(OpResults.Ok),
            parameterCount: 0,
            genericArity: 0,
            method => method.ReturnType == nonGenericType);

        var okWithValue = FindOpResultsFactoryMethod(
            nameof(OpResults.Ok),
            parameterCount: 1,
            genericArity: 1,
            method => IsOpResultOfMethodGenericParameter(method.ReturnType, method.GetGenericArguments().Single()));

        var errWithoutValue = FindOpResultsFactoryMethod(
            nameof(OpResults.Err),
            parameterCount: 1,
            genericArity: 0,
            method => method.ReturnType == nonGenericType);

        var errWithValue = FindOpResultsFactoryMethod(
            nameof(OpResults.Err),
            parameterCount: 1,
            genericArity: 1,
            method => IsOpResultOfMethodGenericParameter(method.ReturnType, method.GetGenericArguments().Single()));

        var okValueParameter = okWithValue.GetParameters().Single();
        var errMessageParameter = errWithoutValue.GetParameters().Single();
        var errValueMessageParameter = errWithValue.GetParameters().Single();

        Assert.NotNull(okWithoutValue);
        Assert.Equal(nonGenericType, errWithoutValue.ReturnType);
        Assert.True(HasDisallowNullAttribute(okValueParameter));
        Assert.True(IsNullableAnnotated(errMessageParameter));
        Assert.True(IsNullableAnnotated(errValueMessageParameter));
        Assert.NotEqual(typeof(OpError), errWithoutValue.ReturnType);
    }

    [Fact]
    public void DefaultResults_AreErrWithEmptyMessage()
    {
        var nonGenericType = GetNonGenericOpResultType();
        var nonGenericDefault = Activator.CreateInstance(nonGenericType)!;

        OpResult<int> genericDefault = default;

        Assert.False(ReadIsOk(nonGenericDefault));
        Assert.True(ReadIsErr(nonGenericDefault));
        Assert.Equal(string.Empty, ReadErrorMessage(nonGenericDefault));
        Assert.False(genericDefault.IsOk);
        Assert.True(genericDefault.IsErr);
        Assert.Equal(string.Empty, genericDefault.Error!.Message);
    }

    [Fact]
    public void ErrBranch_ValueFallbackReturnsDefaultWithoutThrow()
    {
        var errFactory = FindOpResultsFactoryMethod(
            nameof(OpResults.Err),
            parameterCount: 1,
            genericArity: 1,
            method => IsOpResultOfMethodGenericParameter(method.ReturnType, method.GetGenericArguments().Single()));

        var errInt = (OpResult<int>)errFactory.MakeGenericMethod(typeof(int)).Invoke(null, new object?[] { "failed" })!;
        var errString = (OpResult<string>)errFactory.MakeGenericMethod(typeof(string)).Invoke(null, new object?[] { "failed" })!;

        var intReadException = Record.Exception(() => _ = errInt.Value);
        var stringReadException = Record.Exception(() => _ = errString.Value);

        Assert.Null(intReadException);
        Assert.Null(stringReadException);
        Assert.Equal(default, errInt.Value);
        Assert.Equal(default, errString.Value);
    }

    [Fact]
    public void OkBranch_ErrorFallbackReturnsEmptyMessageWithoutThrow()
    {
        var nonGenericType = GetNonGenericOpResultType();
        var okFactory = FindOpResultsFactoryMethod(
            nameof(OpResults.Ok),
            parameterCount: 0,
            genericArity: 0,
            method => method.ReturnType == nonGenericType);

        var okNonGeneric = okFactory.Invoke(null, null)!;
        var okGeneric = OpResults.Ok(42);

        var nonGenericReadException = Record.Exception(() => _ = ReadErrorMessage(okNonGeneric));
        var genericReadException = Record.Exception(() => _ = okGeneric.Error);

        Assert.Null(nonGenericReadException);
        Assert.Null(genericReadException);
        Assert.Equal(string.Empty, ReadErrorMessage(okNonGeneric));
        Assert.Equal(string.Empty, okGeneric.Error!.Message);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void OpErrorMessage_NormalizesNullOrWhitespaceToEmpty(string? message)
    {
        var nonGenericType = GetNonGenericOpResultType();
        var errFactory = FindOpResultsFactoryMethod(
            nameof(OpResults.Err),
            parameterCount: 1,
            genericArity: 0,
            method => method.ReturnType == nonGenericType);

        var err = errFactory.Invoke(null, new object?[] { message })!;
        Assert.Equal(string.Empty, ReadErrorMessage(err));
    }

    [Fact]
    public void ImplicitValueToOpResultOfT_IsRetainedAndRejectsNull()
    {
        OpResult<string> ok = "ready";

        Assert.True(ok.IsOk);
        Assert.False(ok.IsErr);
        Assert.Equal("ready", ok.Value);

        var exception = Assert.Throws<ArgumentNullException>(() =>
        {
            string value = null!;
            OpResult<string> result = value;
            _ = result;
        });

        Assert.Equal("value", exception.ParamName);
    }

    [Fact]
    public void OkFactory_RejectsNullReferencePayload()
    {
        var exception = Assert.Throws<ArgumentNullException>(() =>
            OpResults.Ok<string>(null!));

        Assert.Equal("value", exception.ParamName);
    }

    [Fact]
    public void OpErrorToOpResultImplicitConversions_DoNotExist()
    {
        var implicitOperators = typeof(OpResult<int>)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(method => method.Name == "op_Implicit" && method.ReturnType == typeof(OpResult<int>))
            .ToArray();

        Assert.DoesNotContain(implicitOperators, method =>
            method.GetParameters().Single().ParameterType == typeof(OpError));

        var nonGenericType = GetNonGenericOpResultType();
        var nonGenericImplicitOperators = nonGenericType
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(method => method.Name == "op_Implicit" && method.ReturnType == nonGenericType)
            .ToArray();

        Assert.DoesNotContain(nonGenericImplicitOperators, method =>
            method.GetParameters().Single().ParameterType == typeof(OpError));

        var opErrorImplicitOperators = typeof(OpError)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(method => method.Name == "op_Implicit")
            .ToArray();

        Assert.DoesNotContain(opErrorImplicitOperators, method =>
            method.ReturnType == typeof(OpResult) ||
            method.ReturnType.IsGenericType &&
            method.ReturnType.GetGenericTypeDefinition() == typeof(OpResult<>));
    }

    private static Type GetNonGenericOpResultType()
    {
        var nonGenericType = typeof(OpResults).Assembly.GetType("OpResult.OpResult");
        Assert.NotNull(nonGenericType);
        return nonGenericType!;
    }

    private static MethodInfo FindOpResultsFactoryMethod(
        string name,
        int parameterCount,
        int genericArity,
        Func<MethodInfo, bool> predicate)
    {
        var method = typeof(OpResults)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(candidate =>
                candidate.Name == name &&
                candidate.GetParameters().Length == parameterCount &&
                candidate.GetGenericArguments().Length == genericArity &&
                predicate(candidate))
            .SingleOrDefault();

        Assert.NotNull(method);
        return method!;
    }

    private static bool IsOpResultOfMethodGenericParameter(Type candidate, Type methodGenericParameter) =>
        candidate.IsGenericType &&
        candidate.GetGenericTypeDefinition() == typeof(OpResult<>) &&
        candidate.GetGenericArguments().Single() == methodGenericParameter;

    private static bool HasDisallowNullAttribute(ParameterInfo parameter) =>
        parameter.GetCustomAttributes(typeof(DisallowNullAttribute), inherit: false).Any();

    private static bool IsNullableAnnotated(ParameterInfo parameter) =>
        new NullabilityInfoContext().Create(parameter).WriteState is NullabilityState.Nullable;

    private static bool ReadIsOk(object result) => ReadBoolProperty(result, "IsOk");

    private static bool ReadIsErr(object result) => ReadBoolProperty(result, "IsErr");

    private static string ReadErrorMessage(object result)
    {
        var errorProperty = result.GetType().GetProperty("Error", BindingFlags.Public | BindingFlags.Instance);
        Assert.NotNull(errorProperty);

        var error = errorProperty!.GetValue(result);
        Assert.NotNull(error);

        var messageProperty = error!.GetType().GetProperty(nameof(OpError.Message), BindingFlags.Public | BindingFlags.Instance);
        Assert.NotNull(messageProperty);

        var message = messageProperty!.GetValue(error);
        Assert.IsType<string>(message);
        return (string)message!;
    }

    private static bool ReadBoolProperty(object result, string name)
    {
        var property = result.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
        Assert.NotNull(property);

        var value = property!.GetValue(result);
        Assert.IsType<bool>(value);
        return (bool)value!;
    }
}
