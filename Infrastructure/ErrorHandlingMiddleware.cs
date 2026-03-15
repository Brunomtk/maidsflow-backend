using Core.Exceptions;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;
using System.Net;

namespace Infrastructure
{
	public class ErrorHandlingMiddleware
	{
		private readonly RequestDelegate next;

		public ErrorHandlingMiddleware(RequestDelegate next)
		{
			this.next = next;
		}

		public async Task Invoke(HttpContext context /* other dependencies */)
		{
			try
			{
				await next(context);
			}
			catch (Exception ex)
			{
				await HandleExceptionAsync(context, ex);
			}
		}

		private static Task HandleExceptionAsync(HttpContext context, Exception exception)
		{
			var code = exception switch
			{
				BadRequestException => HttpStatusCode.BadRequest,
				ForbiddenException => HttpStatusCode.Forbidden,
				NotFoundException => HttpStatusCode.NotFound,
				ConflictException => HttpStatusCode.Conflict,
				BadGatewayException => HttpStatusCode.BadGateway,
				_ => HttpStatusCode.InternalServerError
			};

			var payload = new
			{
				message = exception.Message,
				status = (int)code
			};
			var result = JsonConvert.SerializeObject(payload);
			context.Response.ContentType = "application/json";
			context.Response.StatusCode = (int)code;
			return context.Response.WriteAsync(result);
		}
	}
}
