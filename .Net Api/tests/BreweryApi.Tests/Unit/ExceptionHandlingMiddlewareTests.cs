using System.Text.Json;
using System.Net;
using BreweryApi.Domain.Exceptions;
using BreweryApi.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Polly.Timeout;
using Xunit;

namespace BreweryApi.Tests.Unit;

public sealed class ExceptionHandlingMiddlewareTests
{
    public static IEnumerable<object[]> ExceptionCases =>
    [
        [new ArgumentException("invalid"), StatusCodes.Status400BadRequest, "Invalid request", "One or more request parameters are invalid."],
        [new JsonException("invalid json"), StatusCodes.Status502BadGateway, "Invalid external response", "Open Brewery DB returned an invalid response."],
        [new HttpRequestException("unavailable"), StatusCodes.Status503ServiceUnavailable, "External service unavailable", "Open Brewery DB is currently unavailable."],
        [new HttpRequestException("upstream rejected request", null, HttpStatusCode.BadRequest), StatusCodes.Status502BadGateway, "External service rejected request", "Open Brewery DB rejected the request."],
        [new HttpRequestException("upstream unavailable", null, HttpStatusCode.ServiceUnavailable), StatusCodes.Status503ServiceUnavailable, "External service unavailable", "Open Brewery DB is currently unavailable."],
        [new TimeoutException("late"), StatusCodes.Status504GatewayTimeout, "External service timeout", "Open Brewery DB did not respond within the expected time."],
        [new TimeoutRejectedException("resilience timeout"), StatusCodes.Status504GatewayTimeout, "External service timeout", "Open Brewery DB did not respond within the expected time."],
        [new TaskCanceledException("upstream timed out"), StatusCodes.Status504GatewayTimeout, "External service timeout", "Open Brewery DB did not respond within the expected time."],
        [new InvalidOperationException("unexpected"), StatusCodes.Status500InternalServerError, "Unexpected error", "An unexpected error occurred while processing the request."]
    ];

    [Fact]
    public async Task InvokeAsync_WhenRequestValidationExceptionIsThrown_WritesSingleValidProblemDetailsPayload()
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        var middleware = new ExceptionHandlingMiddleware(
            _ => throw new RequestValidationException("Page must be greater than or equal to 1."),
            NullLogger<ExceptionHandlingMiddleware>.Instance);

        await middleware.InvokeAsync(context);

        context.Response.Body.Position = 0;
        using var reader = new StreamReader(context.Response.Body);
        var payload = await reader.ReadToEndAsync();

        using var document = JsonDocument.Parse(payload);
        var root = document.RootElement;

        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
        Assert.Equal("Validation failed", root.GetProperty("title").GetString());
        Assert.Equal(StatusCodes.Status400BadRequest, root.GetProperty("status").GetInt32());
        Assert.Equal("Page must be greater than or equal to 1.", root.GetProperty("detail").GetString());
    }

    [Theory]
    [MemberData(nameof(ExceptionCases))]
    public async Task InvokeAsync_KnownFailure_WritesMappedProblemDetails(
        Exception exception,
        int expectedStatus,
        string expectedTitle,
        string expectedDetail)
    {
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/v1/breweries";
        context.TraceIdentifier = "trace-test";
        context.Response.Body = new MemoryStream();
        var middleware = new ExceptionHandlingMiddleware(
            _ => throw exception,
            NullLogger<ExceptionHandlingMiddleware>.Instance);

        await middleware.InvokeAsync(context);

        context.Response.Body.Position = 0;
        using var document = await JsonDocument.ParseAsync(context.Response.Body);
        var root = document.RootElement;

        Assert.Equal(expectedStatus, context.Response.StatusCode);
        Assert.StartsWith("application/problem+json", context.Response.ContentType, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(expectedTitle, root.GetProperty("title").GetString());
        Assert.Equal(expectedDetail, root.GetProperty("detail").GetString());
        Assert.Equal("/api/v1/breweries", root.GetProperty("instance").GetString());
        Assert.Equal("trace-test", root.GetProperty("traceId").GetString());
    }

    [Fact]
    public async Task InvokeAsync_RequestCancellation_DoesNotWriteErrorResponse()
    {
        var context = new DefaultHttpContext();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        context.RequestAborted = cancellation.Token;
        context.Response.Body = new MemoryStream();
        var middleware = new ExceptionHandlingMiddleware(
            _ => throw new OperationCanceledException(),
            NullLogger<ExceptionHandlingMiddleware>.Instance);

        await middleware.InvokeAsync(context);

        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        Assert.Equal(0, context.Response.Body.Length);
    }

    [Fact]
    public async Task InvokeAsync_ResponseAlreadyStarted_RethrowsOriginalException()
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        var responseFeature = new Mock<IHttpResponseFeature>();
        responseFeature.SetupGet(feature => feature.HasStarted).Returns(true);
        context.Features.Set(responseFeature.Object);
        var exception = new InvalidOperationException("response already started");
        var middleware = new ExceptionHandlingMiddleware(
            _ => throw exception,
            NullLogger<ExceptionHandlingMiddleware>.Instance);

        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(
            () => middleware.InvokeAsync(context));

        Assert.Same(exception, thrown);
    }
}
