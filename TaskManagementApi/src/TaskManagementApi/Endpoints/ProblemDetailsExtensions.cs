using TaskManagementApi.Common.Results;

namespace TaskManagementApi.Endpoints;

/// <summary>
/// Maps Result&lt;T&gt; and Error structs to RFC 9457 ProblemDetails responses using C# pattern matching and modern switch expressions.
/// </summary>
public static class ProblemDetailsExtensions
{
    public static IResult ToProblemDetails(this Error error, HttpContext httpContext)
    {
        (int statusCode, string title, string typeUri) = error.Type switch
        {
            ErrorType.Validation => (
                StatusCodes.Status400BadRequest,
                "One or more validation errors occurred.",
                "https://tools.ietf.org/html/rfc9110#section-15.5.1"),
            ErrorType.NotFound => (
                StatusCodes.Status404NotFound,
                "Resource Not Found",
                "https://tools.ietf.org/html/rfc9110#section-15.5.5"),
            ErrorType.Conflict => (
                StatusCodes.Status409Conflict,
                "Resource Conflict",
                "https://tools.ietf.org/html/rfc9110#section-15.5.10"),
            _ => (
                StatusCodes.Status500InternalServerError,
                "Internal Server Error",
                "https://tools.ietf.org/html/rfc9110#section-15.6.1")
        };

        var extensions = new Dictionary<string, object?>(2, StringComparer.Ordinal)
        {
            ["errorCode"] = error.Code,
            ["traceId"] = httpContext.TraceIdentifier
        };

        return error switch
        {
            { Type: ErrorType.Validation, ValidationErrors: { Count: > 0 } validationErrors } =>
                TypedResults.ValidationProblem(
                    errors: validationErrors,
                    detail: error.Description,
                    instance: httpContext.Request.Path,
                    title: title,
                    type: typeUri,
                    extensions: extensions),
            _ =>
                TypedResults.Problem(
                    detail: error.Description,
                    instance: httpContext.Request.Path,
                    statusCode: statusCode,
                    title: title,
                    type: typeUri,
                    extensions: extensions)
        };
    }
}
