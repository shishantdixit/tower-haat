using Microsoft.AspNetCore.Diagnostics;
using TowerHaat.Application.Common;

namespace TowerHaat.Api.Errors;

/// <summary>Turns expected failures into problem details whose detail is shown to the user.</summary>
internal sealed class AppExceptionHandler(IProblemDetailsService problemDetails) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is not AppException app)
        {
            return false;
        }

        context.Response.StatusCode = app.Kind switch
        {
            ErrorKind.Validation => StatusCodes.Status400BadRequest,
            ErrorKind.Unauthorized => StatusCodes.Status401Unauthorized,
            ErrorKind.Forbidden => StatusCodes.Status403Forbidden,
            ErrorKind.NotFound => StatusCodes.Status404NotFound,
            ErrorKind.Conflict => StatusCodes.Status409Conflict,
            ErrorKind.TooManyRequests => StatusCodes.Status429TooManyRequests,
            _ => StatusCodes.Status400BadRequest,
        };

        if (app.RetryAfter is { } retryAfter)
        {
            context.Response.Headers.RetryAfter = Math.Ceiling(retryAfter.TotalSeconds).ToString("0");
        }

        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            Exception = exception,
            ProblemDetails = { Status = context.Response.StatusCode, Detail = app.Message },
        });
    }
}
