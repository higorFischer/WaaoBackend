using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Waao.API.Middleware;
using Waao.Services.Abstractions;
using Xunit;

namespace Waao.Tests.Auth;

public sealed class ExceptionMappingTests
{
	private static async Task<(int status, string body)> Run(Exception ex)
	{
		var ctx = new DefaultHttpContext();
		ctx.Response.Body = new MemoryStream();
		var mw = new ExceptionHandlingMiddleware(_ => throw ex, NullLogger<ExceptionHandlingMiddleware>.Instance);
		await mw.InvokeAsync(ctx);
		ctx.Response.Body.Position = 0;
		return (ctx.Response.StatusCode, await new StreamReader(ctx.Response.Body).ReadToEndAsync());
	}

	[Fact]
	public async Task EmailNotVerified_Maps_403_WithCode()
	{
		var (status, body) = await Run(new EmailNotVerifiedException("a@waao.com.br"));
		status.Should().Be(403);
		JsonDocument.Parse(body).RootElement.GetProperty("code").GetString().Should().Be("email_not_verified");
	}

	[Fact]
	public async Task InvalidToken_Maps_400_WithCode()
	{
		var (status, body) = await Run(new InvalidVerificationTokenException());
		status.Should().Be(400);
		JsonDocument.Parse(body).RootElement.GetProperty("code").GetString().Should().Be("invalid_or_expired_token");
	}
}
