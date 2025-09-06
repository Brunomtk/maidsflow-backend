using Microsoft.AspNetCore.Mvc;
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
            context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
            context.Response.ContentType = "application/json";

            var problemDetails = new ProblemDetails
            {
                Status = context.Response.StatusCode,
                Title = "Internal Server Error",
                Detail = exception.Message
            };

            await context.Response.WriteAsJsonAsync(problemDetails);
        }
    }
}
