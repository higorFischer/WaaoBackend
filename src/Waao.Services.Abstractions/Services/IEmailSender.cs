namespace Waao.Services.Abstractions.Services;

public interface IEmailSender
{
	Task SendVerificationAsync(string toEmail, string toName, string verifyUrl, CancellationToken ct = default);
}
