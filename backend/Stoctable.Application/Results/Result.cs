namespace Stoctable.Application.Results;

public class Result<T>
{
    public T? Data { get; private init; }
    public bool IsSuccess { get; private init; }
    public string? ErrorMessage { get; private init; }
    public int StatusCode { get; private init; }

    public static Result<T> Success(T data, int statusCode = 200)
        => new() { Data = data, IsSuccess = true, StatusCode = statusCode };

    public static Result<T> Failure(string errorMessage, int statusCode = 400)
        => new() { IsSuccess = false, ErrorMessage = errorMessage, StatusCode = statusCode };

    public static Result<T> NotFound(string errorMessage = "Registro não encontrado.")
        => Failure(errorMessage, 404);

    public static Result<T> Unauthorized(string errorMessage = "Não autorizado.")
        => Failure(errorMessage, 401);

    public static Result<T> Conflict(string errorMessage)
        => Failure(errorMessage, 409);
}
