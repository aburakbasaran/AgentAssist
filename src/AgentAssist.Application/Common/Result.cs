namespace AgentAssist.Application.Common;

/// <summary>
/// Represents the outcome of an application operation.
/// </summary>
/// <typeparam name="T">The successful value type.</typeparam>
public sealed record Result<T>
{
    private Result(T value)
    {
        Value = value;
        IsSuccess = true;
        Error = string.Empty;
    }

    private Result(string error)
    {
        Value = default;
        IsSuccess = false;
        Error = error;
    }

    /// <summary>
    /// Gets a value indicating whether the operation succeeded.
    /// </summary>
    public bool IsSuccess { get; }

    /// <summary>
    /// Gets the successful value, when available.
    /// </summary>
    public T? Value { get; }

    /// <summary>
    /// Gets the error message for failed outcomes.
    /// </summary>
    public string Error { get; }

    /// <summary>
    /// Creates a successful result.
    /// </summary>
    /// <param name="value">The successful value.</param>
    /// <returns>A successful result.</returns>
    public static Result<T> Success(T value) => new(value);

    /// <summary>
    /// Creates a failed result.
    /// </summary>
    /// <param name="error">The failure message.</param>
    /// <returns>A failed result.</returns>
    public static Result<T> Failure(string error) => new(error);
}
