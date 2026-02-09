namespace OpResult;

/// <summary>
/// Optional protocol for error types to provide standardized error information.
/// Does not participate in any generic constraints.
/// </summary>
public interface IOpError
{
    /// <summary>
    /// Gets the error code.
    /// </summary>
    string Code { get; }

    /// <summary>
    /// Gets the error message.
    /// </summary>
    string Message { get; }
}
