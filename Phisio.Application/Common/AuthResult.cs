namespace Phisio.Application.Common;

public sealed class AuthResult<T>
{
    public bool Succeeded { get; private init; }

    public T? Value { get; private init; }

    public IReadOnlyList<string> Errors { get; private init; } = [];

    public static AuthResult<T> Success(T value) =>
        new() { Succeeded = true, Value = value };

    public static AuthResult<T> Failure(IEnumerable<string> errors) =>
        new() { Succeeded = false, Errors = errors.ToList() };
}
