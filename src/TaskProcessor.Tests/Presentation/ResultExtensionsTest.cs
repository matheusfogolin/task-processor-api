using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using TaskProcessor.Domain.Shared;
using TaskProcessor.Presentation.Extensions;

namespace TaskProcessor.Tests.Presentation;

public class ResultExtensionsTest
{
    [Fact]
    public void ToErrorResult_WithValidationErrors_ShouldReturn400()
    {
        Result<string> result = DomainError.Validation("Field.Required", "Field is required.");

        var objectResult = result.ToErrorResult();

        objectResult.StatusCode.Should().Be(400);
        var problem = objectResult.Value.Should().BeOfType<ProblemDetails>().Subject;
        problem.Title.Should().Be("Validation Error");
        problem.Detail.Should().Be("One or more validation errors occurred.");
        problem.Extensions.Should().ContainKey("errors");
    }

    [Fact]
    public void ToErrorResult_WithMultipleValidationErrors_ShouldReturn400WithAllErrors()
    {
        Result<string> result = new List<DomainError>
        {
            DomainError.Validation("Field.Required", "Field is required."),
            DomainError.Validation("Field.TooLong", "Field is too long.")
        };

        var objectResult = result.ToErrorResult();

        objectResult.StatusCode.Should().Be(400);
        var problem = objectResult.Value.Should().BeOfType<ProblemDetails>().Subject;
        problem.Extensions.Should().ContainKey("errors");
        var errors = problem.Extensions["errors"] as System.Collections.IList;
        errors.Should().NotBeNull();
        errors!.Count.Should().Be(2);
    }

    [Fact]
    public void ToErrorResult_WithNotFoundError_ShouldReturn404()
    {
        Result<string> result = DomainError.NotFound("Job.NotFound", "Job not found.");

        var objectResult = result.ToErrorResult();

        objectResult.StatusCode.Should().Be(404);
        var problem = objectResult.Value.Should().BeOfType<ProblemDetails>().Subject;
        problem.Title.Should().Be("Not Found");
        problem.Detail.Should().Be("Job not found.");
    }

    [Fact]
    public void ToErrorResult_WithConflictError_ShouldReturn409()
    {
        Result<string> result = DomainError.Conflict("Job.Conflict", "Job already exists.");

        var objectResult = result.ToErrorResult();

        objectResult.StatusCode.Should().Be(409);
        var problem = objectResult.Value.Should().BeOfType<ProblemDetails>().Subject;
        problem.Title.Should().Be("Conflict");
        problem.Detail.Should().Be("Job already exists.");
    }

    [Fact]
    public void ToErrorResult_WithFailureError_ShouldReturn500()
    {
        Result<string> result = DomainError.Failure("Job.Failure", "Unexpected error.");

        var objectResult = result.ToErrorResult();

        objectResult.StatusCode.Should().Be(500);
        var problem = objectResult.Value.Should().BeOfType<ProblemDetails>().Subject;
        problem.Title.Should().Be("Internal Server Error");
        problem.Detail.Should().Be("Unexpected error.");
    }
}
