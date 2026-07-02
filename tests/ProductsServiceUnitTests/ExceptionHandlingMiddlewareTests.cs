using System.Diagnostics;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;
using ProductsMicroService.API.Middleware;

namespace ProductsMicroservice.Tests;

public class ExceptionHandlingMiddlewareTests
{
    private readonly Mock<ILogger<ExceptionHandlingMiddleware>> _loggerMock = new();

    [Fact]
    public async Task Invoke_ShouldCallNextMiddleware_WhenNoExceptionOccurs()
    {
        var nextCalled = false;
        var middleware = new ExceptionHandlingMiddleware(
            _ =>
            {
                nextCalled = true;
                return Task.CompletedTask;
            },
            _loggerMock.Object);

        await middleware.Invoke(new DefaultHttpContext());

        nextCalled.Should().BeTrue();
    }

    [Fact]
    public async Task Invoke_ShouldReturnStructuredError_WhenNextMiddlewareThrows()
    {
        var exception = new InvalidOperationException("outer", new Exception("inner"));
        var middleware = new ExceptionHandlingMiddleware(_ => throw exception, _loggerMock.Object);
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        using var activity = new Activity("middleware-test").Start();

        await middleware.Invoke(context);

        context.Response.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);
        context.Response.ContentType.Should().StartWith("application/json");
        context.Response.Body.Position = 0;
        using var json = await JsonDocument.ParseAsync(context.Response.Body);
        json.RootElement.GetProperty("message").GetString().Should().Be("Internal Server Error");
        json.RootElement.GetProperty("type").GetString().Should().Be(nameof(InvalidOperationException));
        json.RootElement.GetProperty("detail").GetString().Should().Be("inner");
        activity.Status.Should().Be(ActivityStatusCode.Error);
    }
}
