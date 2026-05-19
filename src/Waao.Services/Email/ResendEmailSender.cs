using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Waao.Services.Abstractions.Services;

namespace Waao.Services.Email;

public sealed class ResendEmailSender(HttpClient Http, ILogger<ResendEmailSender> Logger, string ApiKey, string From) : IEmailSender
{
	public async Task SendVerificationAsync(string toEmail, string toName, string verifyUrl, CancellationToken ct = default)
	{
		var html = $"""
			<div style="font-family:system-ui,sans-serif;max-width:480px;margin:auto">
			  <h2>Welcome to WAAO Journey</h2>
			  <p>Hi {System.Net.WebUtility.HtmlEncode(toName)}, confirm your email to activate your account.</p>
			  <p><a href="{System.Net.WebUtility.HtmlEncode(verifyUrl)}" style="background:#6366F1;color:#fff;padding:10px 18px;border-radius:8px;text-decoration:none">Verify my email</a></p>
			  <p style="color:#64748B;font-size:12px">Or paste this link: {System.Net.WebUtility.HtmlEncode(verifyUrl)}<br/>This link expires in 24 hours.</p>
			</div>
			""";
		using var req = new HttpRequestMessage(HttpMethod.Post, "https://api.resend.com/emails")
		{
			Content = JsonContent.Create(new { from = From, to = (string[])[toEmail], subject = "Verify your WAAO email", html }),
		};
		req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ApiKey);
		var res = await Http.SendAsync(req, ct);
		if (!res.IsSuccessStatusCode)
		{
			var msg = await res.Content.ReadAsStringAsync(ct);
			Logger.LogError("Resend send failed {Status}: {Body}", (int)res.StatusCode, msg);
			throw new InvalidOperationException($"Resend returned {(int)res.StatusCode}");
		}
	}
}
