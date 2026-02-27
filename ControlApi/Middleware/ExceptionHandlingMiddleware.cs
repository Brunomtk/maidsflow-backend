using Microsoft.AspNetCore.Mvc;
using Core.Exceptions;
using System.Net;

namespace ControlApi.Middleware
{
    public class ExceptionHandlingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<ExceptionHandlingMiddleware> _logger;

        public ExceptionHandlingMiddleware(
            RequestDelegate next,
            ILogger<ExceptionHandlingMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        //public async Task InvokeAsync(HttpContext context)
        //{
        //    try
        //    {
        //        await _next(context);
        //    }
        //    catch (Exception exception)
        //    {
        //        _logger.LogError(
        //            exception, "Exception occurred: {Message}", exception.Message);

        //        var problemDetails = new ProblemDetails
        //        {
        //            Status = StatusCodes.Status500InternalServerError,
        //            Title = "Server Error"
        //        };

        //        context.Response.StatusCode =
        //            StatusCodes.Status500InternalServerError;

        //        await context.Response.WriteAsJsonAsync(problemDetails);
        //    }
        //}

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled exception occurred: {Message}", ex.Message);
                await HandleExceptionAsync(context, ex);
            }
        }

                private async Task HandleExceptionAsync(HttpContext context, Exception exception)
        {
            context.Response.ContentType = "application/json";

            var (statusCode, title) = exception switch
            {
                ForbiddenException => ((int)HttpStatusCode.Forbidden, "Forbidden"),
                UnauthorizedAccessException => ((int)HttpStatusCode.Unauthorized, "Unauthorized"),
                BadRequestException => ((int)HttpStatusCode.BadRequest, "Bad Request"),
                ConflictException => ((int)HttpStatusCode.Conflict, "Conflict"),
                BadGatewayException => ((int)HttpStatusCode.BadGateway, "Bad Gateway"),
                ArgumentException => ((int)HttpStatusCode.BadRequest, "Bad Request"),
                NotFoundException => ((int)HttpStatusCode.NotFound, "Not Found"),
                KeyNotFoundException => ((int)HttpStatusCode.NotFound, "Not Found"),
                _ => ((int)HttpStatusCode.InternalServerError, "Internal Server Error")
            };

            context.Response.StatusCode = statusCode;

            var problemDetails = new ProblemDetails
            {
                Status = statusCode,
                Title = title,
                Detail = exception.Message
            };

            await context.Response.WriteAsJsonAsync(problemDetails);
        }
    }
}

