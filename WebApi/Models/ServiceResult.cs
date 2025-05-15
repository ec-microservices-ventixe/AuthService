namespace WebApi.Models;

public class ServiceResult<TData>
{
    public bool Success { get; protected set; }
    public int StatusCode { get; protected set; }
    public TData? Data { get; protected set; }
    public string? ErrorMessage { get; protected set; }

    private ServiceResult(bool success, int statusCode, TData? data = default, string? errorMessage = null)
    {
        Success = success;
        StatusCode = statusCode;
        Data = data;
        ErrorMessage = errorMessage;
    }
    public static ServiceResult<TData> Ok(TData data)
    {
        return new ServiceResult<TData>(true, 200, data);
    }
    public static ServiceResult<TData> Ok(string text)
    {
        return new ServiceResult<TData>(true, 200);
    }
    public static ServiceResult<TData> Created(TData data)
    {
        return new ServiceResult<TData>(true, 201, data);
    }
    public static ServiceResult<TData> NoContent()
    {
        return new ServiceResult<TData>(true, 204);
    }

    public static ServiceResult<TData> BadRequest(string message)
    {
        return new ServiceResult<TData>(false, 400, default, message);
    }

    public static ServiceResult<TData> NotFound(string message)
    {
        return new ServiceResult<TData>(false, 404, default, message);
    }

    public static ServiceResult<TData> Conflict(string message)
    {
        return new ServiceResult<TData>(false, 409, default, message);
    }

    public static ServiceResult<TData> Error(string message)
    {
        return new ServiceResult<TData>(false, 500, default, message);
    }

    public static ServiceResult<TData> Unauthorized(string message)
    {
        return new ServiceResult<TData>(false, 401, default, message);
    }

    public static ServiceResult<TData> Forbidden(string message)
    {
        return new ServiceResult<TData>(false, 403, default, message);
    }
}