using System.Text.Json;
using FluentValidation;

namespace Waao.API.Middleware;

public sealed class ExceptionHandlingMiddleware(RequestDelegate Next, ILogger<ExceptionHandlingMiddleware> Logger)
{
	private static readonly JsonSerializerOptions JsonOptions = new()
	{
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
	};

	public async Task InvokeAsync(HttpContext context)
	{
		try
		{
			await Next(context);
		}
		catch (ValidationException ex)
		{
			context.Response.StatusCode = StatusCodes.Status400BadRequest;
			context.Response.ContentType = "application/problem+json";

			var errors = ex.Errors
				.GroupBy(e => e.PropertyName)
				.ToDictionary(
					g => string.IsNullOrEmpty(g.Key) ? "_" : ToCamelCase(g.Key),
					g => g.Select(e => e.ErrorMessage).ToArray());

			var payload = new
			{
				type = "https://datatracker.ietf.org/doc/html/rfc9110#name-400-bad-request",
				title = "Validation failed",
				status = 400,
				errors,
			};
			await context.Response.WriteAsync(JsonSerializer.Serialize(payload, JsonOptions));
		}
		catch (UnauthorizedAccessException ex)
		{
			context.Response.StatusCode = StatusCodes.Status401Unauthorized;
			context.Response.ContentType = "application/problem+json";
			var payload = new
			{
				type = "https://datatracker.ietf.org/doc/html/rfc9110#name-401-unauthorized",
				title = "Unauthorized",
				status = 401,
				detail = ex.Message,
			};
			await context.Response.WriteAsync(JsonSerializer.Serialize(payload, JsonOptions));
		}
		catch (KeyNotFoundException ex)
		{
			context.Response.StatusCode = StatusCodes.Status404NotFound;
			context.Response.ContentType = "application/problem+json";
			var payload = new
			{
				type = "https://datatracker.ietf.org/doc/html/rfc9110#name-404-not-found",
				title = "Not found",
				status = 404,
				detail = ex.Message,
			};
			await context.Response.WriteAsync(JsonSerializer.Serialize(payload, JsonOptions));
		}
		catch (Exception ex)
		{
			Logger.LogError(ex, "Unhandled exception on {Path}", context.Request.Path);
			context.Response.StatusCode = StatusCodes.Status500InternalServerError;
			context.Response.ContentType = "application/problem+json";
			var payload = new
			{
				type = "https://datatracker.ietf.org/doc/html/rfc9110#name-500-internal-server-error",
				title = "Unexpected error",
				status = 500,
			};
			await context.Response.WriteAsync(JsonSerializer.Serialize(payload, JsonOptions));
		}
	}

	private static string ToCamelCase(string path)
	{
		// FluentValidation returns e.g. "FullName" or "Birthdate"; lowercase the first char of each dotted segment.
		return string.Join('.', path.Split('.').Select(s =>
			string.IsNullOrEmpty(s) ? s : char.ToLowerInvariant(s[0]) + s[1..]));
	}
}
