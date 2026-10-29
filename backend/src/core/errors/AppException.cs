namespace DotnetSvelte.Core.Errors;

public class AppException(int statusCode, string detail, bool bearerChallenge = false) : Exception(detail)
{
    public int StatusCode { get; } = statusCode;
    public string Detail { get; } = detail;
    public bool BearerChallenge { get; } = bearerChallenge;

    public static AppException BadRequest(string detail) => new(400, detail);
    public static AppException NotAuthenticated() => new(401, "Not authenticated", true);
    public static AppException InvalidCredentials() => new(401, "Could not validate credentials", true);
    public static AppException Forbidden(string detail) => new(403, detail);
    public static AppException NotFound(string detail) => new(404, detail);
    public static AppException Conflict(string detail) => new(409, detail);
    public static AppException Invalid(string detail) => new(422, detail);
    public static AppException Unavailable(string detail) => new(503, detail);
}
