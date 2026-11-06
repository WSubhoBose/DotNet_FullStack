using System.Text.Json;
using System.Net;
using BreweryApi.Domain.Exceptions;
using Microsoft.AspNetCore.Mvc;
using Polly.Timeout;

namespace BreweryApi.Middleware;

/// <summary>Converts application and upstream-service exceptions into Problem Details responses.</summary>
/// <param name="next">The next request delegate in the middleware pipeline.</param>
/// <param name="logger">Logger used to record handled and unhandled request failures.</param>
public sealed class ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
{
    /// <summary>Invokes the next middleware and handles exceptions raised while processing the request.</summary>
    /// <param name="context">The current HTTP request and response context.</param>
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (RequestValidationException exception)
        {
            await WriteProblemDetailsAsync(
                context,
                exception,
                StatusCodes.Status400BadRequest,
                "Validation failed",
                exception.Message);
        }
        catch (ArgumentException exception)
        {
            await WriteProblemDetailsAsync(
                context,
                exception,
                StatusCodes.Status400BadRequest,
                "Invalid request",
                "One or more request parameters are invalid.");
        }
        catch (JsonException exception)
        {
            await WriteProblemDetailsAsync(
                context,
                exception,
                StatusCodes.Status502BadGateway,
                "Invalid external response",
                "Open Brewery DB returned an invalid response.");
        }
        catch (HttpRequestException exception) when (
            exception.StatusCode is { } statusCode &&
            (int)statusCode >= (int)HttpStatusCode.BadRequest &&
            (int)statusCode < (int)HttpStatusCode.InternalServerError)
        {
            await WriteProblemDetailsAsync(
                context,
                exception,
                StatusCodes.Status502BadGateway,
                "External service rejected request",
                "Open Brewery DB rejected the request.");
        }
        catch (HttpRequestException exception)
        {
            await WriteProblemDetailsAsync(
                context,
                exception,
                StatusCodes.Status503ServiceUnavailable,
                "External service unavailable",
                "Open Brewery DB is currently unavailable.");
        }
        catch (Exception exception) when (exception is TimeoutException or TimeoutRejectedException)
        {
            await WriteProblemDetailsAsync(
                context,
                exception,
                StatusCodes.Status504GatewayTimeout,
                "External service timeout",
                "Open Brewery DB did not respond within " +
                "the expected time.");
        }
        catch (OperationCanceledException exception) when (!context.RequestAborted.IsCancellationRequested)
        {
            await WriteProblemDetailsAsync(
            context,
            exception,
            StatusCodes.Status504GatewayTimeout,
            "External service timeout",
            "Open Brewery DB did not respond within " +
            "the expected time.");
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            logger.LogInformation("Request was cancelled. TraceId: {TraceId}", context.TraceIdentifier);
        }
        catch (Exception exception)
        {
            await WriteProblemDetailsAsync(
                context,
                exception,
                StatusCodes.Status500InternalServerError,
                "Unexpected error",
                "An unexpected error occurred while " +
                "processing the request.");
        }
    }

    private async Task WriteProblemDetailsAsync(HttpContext context, Exception exception, int statusCode, string title, string detail)
    {
        if (context.Response.HasStarted)
        {
            logger.LogError(exception, "The response has already started. TraceId: {TraceId}", context.TraceIdentifier);

            throw exception;
        }

        LogException(context, exception, statusCode);

        context.Response.Clear();
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/problem+json";

        var problemDetails =
            new ProblemDetails
            {
                Status = statusCode,
                Title = title,
                Detail = detail,
                Instance = context.Request.Path
            };

        problemDetails.Extensions["traceId"] = context.TraceIdentifier;

        await JsonSerializer.SerializeAsync(
            context.Response.Body,
            problemDetails,
            cancellationToken: context.RequestAborted);
    }

    private void LogException(HttpContext context, Exception exception, int statusCode)
    {
        if (statusCode >= 500)
        {
            logger.LogError(
                exception,
                "Request failed. " +
                "StatusCode: {StatusCode}, " +
                "Method: {Method}, " +
                "Path: {Path}, " +
                "TraceId: {TraceId}",
                statusCode,
                context.Request.Method,
                context.Request.Path,
                context.TraceIdentifier
            );

            return;
        }

        logger.LogWarning(
            "Request rejected. " +
            "StatusCode: {StatusCode}, " +
            "Method: {Method}, " +
            "Path: {Path}, " +
            "Message: {ErrorMessage}, " +
            "TraceId: {TraceId}",
            statusCode,
            context.Request.Method,
            context.Request.Path,
            exception.Message,
            context.TraceIdentifier
        );
    }
}