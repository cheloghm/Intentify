namespace Intentify.Shared.Validation;

public enum OperationStatus
{
    Success,
    ValidationFailed,
    Unauthorized,
    Forbidden,
    NotFound,
    Conflict,
    Error
}

public sealed record OperationResult<T>(OperationStatus Status, T? Value = default, ValidationErrors? Errors = null)
{
    public bool IsSuccess => Status == OperationStatus.Success;

    public static OperationResult<T> Success(T value) => new(OperationStatus.Success, value);

    public static OperationResult<T> ValidationFailed(ValidationErrors errors) => new(OperationStatus.ValidationFailed, default, errors);

    public static OperationResult<T> Unauthorized() => new(OperationStatus.Unauthorized);

    public static OperationResult<T> Forbidden() => new(OperationStatus.Forbidden);

    public static OperationResult<T> NotFound() => new(OperationStatus.NotFound);

    public static OperationResult<T> Conflict() => new(OperationStatus.Conflict);

    public static OperationResult<T> Error() => new(OperationStatus.Error);
}
