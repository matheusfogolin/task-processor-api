using Microsoft.AspNetCore.Mvc;
using TaskProcessor.Domain.Shared;

namespace TaskProcessor.Presentation.Extensions;

public static class ResultExtensions
{
    public static ObjectResult ToErrorResult<T>(this Result<T> result)
    {
        var (problemDetails, statusCode) = result.FirstError.Type switch
        {
            EErrorType.Validation => BuildValidationProblem(result.Errors),
            EErrorType.NotFound   => BuildProblem(result.FirstError, 404, "Not Found"),
            EErrorType.Conflict   => BuildProblem(result.FirstError, 409, "Conflict"),
            _                     => BuildProblem(result.FirstError, 500, "Internal Server Error")
        };

        return new ObjectResult(problemDetails) { StatusCode = statusCode };
    }

    private static (ProblemDetails, int) BuildValidationProblem(List<DomainError> errors)
    {
        var problem = new ProblemDetails
        {
            Status = 400,
            Title = "Validation Error",
            Detail = "One or more validation errors occurred."
        };

        problem.Extensions["errors"] = errors
            .Select(e => new { code = e.Code, description = e.Description })
            .ToList();

        return (problem, 400);
    }

    private static (ProblemDetails, int) BuildProblem(DomainError error, int status, string title) =>
        (new ProblemDetails { Status = status, Title = title, Detail = error.Description }, status);
}
