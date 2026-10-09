namespace TTemplate.Api.Infrastructure;

/// <summary>Помилки API в одному форматі: {"error":{"code","message","details"}} — UI (api/http.ts) розбирає його в ApiError.</summary>
public static class ApiErrors {
    public static IResult Error(int status, string code, string message, object? details = null) =>
        Results.Json(new { error = new { code, message, details } }, statusCode: status);

    public static IResult NotFound(string message) => Error(404, "not_found", message);

    public static IResult BadRequest(string message) => Error(400, "bad_request", message);

    /// <summary>Помилка конкретного поля: details.fields.value — UI показує її під полем.</summary>
    public static IResult Validation(string message) =>
        Error(400, "validation_failed", message, new { fields = new { value = message } });
}
