namespace Intentify.Shared.Abstractions;

public record PageRequest(int Page, int PageSize);

public record PageResult<T>(IReadOnlyList<T> Items, int Total, int Page, int PageSize);
