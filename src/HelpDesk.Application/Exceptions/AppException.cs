namespace HelpDesk.Application.Exceptions;

public class AppException : Exception
{
    public int StatusCode { get; }
    public string ErrorCode { get; }

    public AppException(string message, int statusCode = 400, string errorCode = "BAD_REQUEST")
        : base(message)
    {
        StatusCode = statusCode;
        ErrorCode = errorCode;
    }
}

public class NotFoundException : AppException
{
    public NotFoundException(string what) : base($"{what} not found.", 404, "NOT_FOUND") { }
}

public class ValidationException : AppException
{
    public IReadOnlyDictionary<string, string[]> Errors { get; }

    public ValidationException(IReadOnlyDictionary<string, string[]> errors)
        : base("One or more validation errors occurred.", 400, "VALIDATION_ERROR") => Errors = errors;

    public ValidationException(string field, string message)
        : this(new Dictionary<string, string[]> { [field] = new[] { message } }) { }
}

public class ForbiddenException : AppException
{
    public ForbiddenException(string message = "You do not have permission to perform this action.")
        : base(message, 403, "FORBIDDEN") { }
}

public class ConflictException : AppException
{
    public ConflictException(string message) : base(message, 409, "CONFLICT") { }
}

public class UnauthorizedException : AppException
{
    public UnauthorizedException(string message = "Invalid credentials.")
        : base(message, 401, "UNAUTHORIZED") { }
}