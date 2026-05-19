using Microsoft.Extensions.Logging;
using Waao.Services.Abstractions.Services;

namespace Waao.Services.Email;

public sealed class LoggingEmailSender(ILogger<LoggingEmailSender> Logger) : IEmailSender
{
	public Task SendVerificationAsync(string toEmail, string toName, string verifyUrl, CancellationToken ct = default)
	{
		Logger.LogWarning("[DEV email] Verification link for {Email} ({Name}): {Url}", toEmail, toName, verifyUrl);
		return Task.CompletedTask;
	}
}
