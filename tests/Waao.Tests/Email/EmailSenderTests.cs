using System.Net;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Waao.Services.Email;
using Xunit;

namespace Waao.Tests.Email;

public sealed class EmailSenderTests
{
	private sealed class CapturingHandler : HttpMessageHandler
	{
		public HttpRequestMessage? Last;
		public string? Body;
		protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
		{
			Last = request;
			Body = request.Content is null ? null : await request.Content.ReadAsStringAsync(ct);
			return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"id\":\"x\"}") };
		}
	}

	[Fact]
	public async Task Resend_PostsToApi_WithBearerAndPayload()
	{
		var h = new CapturingHandler();
		var sender = new ResendEmailSender(new HttpClient(h), NullLogger<ResendEmailSender>.Instance, "re_test", "WAAO <no-reply@waao.com.br>");
		await sender.SendVerificationAsync("a@waao.com.br", "Alice", "https://x/verify-email?token=abc", default);

		h.Last!.RequestUri!.ToString().Should().Be("https://api.resend.com/emails");
		h.Last!.Headers.Authorization!.ToString().Should().Be("Bearer re_test");
		h.Body.Should().Contain("a@waao.com.br").And.Contain("verify-email?token=abc");
	}

	[Fact]
	public async Task Logging_FallbackDoesNotThrow()
	{
		var sender = new LoggingEmailSender(NullLogger<LoggingEmailSender>.Instance);
		var act = async () => await sender.SendVerificationAsync("a@waao.com.br", "Alice", "https://x/verify-email?token=abc", default);
		await act.Should().NotThrowAsync();
	}
}
