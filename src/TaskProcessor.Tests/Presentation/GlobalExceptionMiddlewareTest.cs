using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using TaskProcessor.Presentation.Middleware;

namespace TaskProcessor.Tests.Presentation;

public class GlobalExceptionMiddlewareTest
{
    private readonly Mock<ILogger<GlobalExceptionMiddleware>> _loggerMock = new();

    [Fact]
    public async Task InvokeAsync_WithException_ShouldReturn500ProblemDetails()
    {
        RequestDelegate throwingDelegate = _ => throw new InvalidOperationException("Test error");
        var middleware = new GlobalExceptionMiddleware(throwingDelegate, _loggerMock.Object);

        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context);

        context.Response.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);
        context.Response.ContentType.Should().Contain("application/problem+json");

        context.Response.Body.Seek(0, SeekOrigin.Begin);
        var problemDetails = await JsonSerializer.DeserializeAsync<ProblemDetails>(
            context.Response.Body,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        problemDetails.Should().NotBeNull();
        problemDetails!.Title.Should().Be("Internal Server Error");
        problemDetails.Detail.Should().Be("An unexpected error occurred. Please try again later.");

        _loggerMock.Invocations.Should().NotBeEmpty();
    }

    [Fact]
    public async Task InvokeAsync_WithoutException_ShouldPassThrough()
    {
        RequestDelegate successDelegate = ctx =>
        {
            ctx.Response.StatusCode = StatusCodes.Status200OK;
            return Task.CompletedTask;
        };
        var middleware = new GlobalExceptionMiddleware(successDelegate, _loggerMock.Object);

        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context);

        context.Response.StatusCode.Should().Be(StatusCodes.Status200OK);
    }
}
