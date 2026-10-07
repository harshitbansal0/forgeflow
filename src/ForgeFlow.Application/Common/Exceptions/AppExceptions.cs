namespace ForgeFlow.Application.Common.Exceptions;

public sealed class NotFoundException(string message) : Exception(message)
{
    public static NotFoundException For(string entity, object key) => new($"{entity} '{key}' was not found.");
}

public sealed class ConflictException(string message) : Exception(message);

public sealed class ForbiddenException(string message) : Exception(message);

public sealed class RequestValidationException(IDictionary<string, string[]> errors)
    : Exception("One or more validation errors occurred.")
{
    public IDictionary<string, string[]> Errors { get; } = errors;

    public RequestValidationException(string field, string error)
        : this(new Dictionary<string, string[]> { [field] = [error] })
    {
    }
}
