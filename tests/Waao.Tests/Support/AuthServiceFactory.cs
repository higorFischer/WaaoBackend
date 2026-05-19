using FluentValidation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Waao.Infra.EF;
using Waao.Services.Abstractions.Dtos;
using Waao.Services.Abstractions.Services;
using Waao.Services.Auth;
using Waao.Services.Gamification;
using Waao.Services.Services;
using Waao.Services.Validation;

namespace Waao.Tests.Support;

/// <summary>
/// Capturing test email sender — records the last send and a count.
/// </summary>
public sealed class CapturingEmailSender : IEmailSender
{
	public int SentCount { get; private set; }
	public (string Email, string Name, string VerifyUrl)? Last { get; private set; }

	public Task SendVerificationAsync(string toEmail, string toName, string verifyUrl, CancellationToken ct = default)
	{
		SentCount++;
		Last = (toEmail, toName, verifyUrl);
		return Task.CompletedTask;
	}
}

public sealed class AuthServiceFactory
{
	public AuthService Service { get; }
	public WaaoDbContext Db { get; }
	public CapturingEmailSender EmailSender { get; }

	public (string Email, string Name, string VerifyUrl)? LastEmail => EmailSender.Last;
	public int SentCount => EmailSender.SentCount;

	private AuthServiceFactory(AuthService service, WaaoDbContext db, CapturingEmailSender emailSender)
	{
		Service = service;
		Db = db;
		EmailSender = emailSender;
	}

	public static AuthServiceFactory Create()
	{
		var db = TestDb.New();

		var jwtSettings = new JwtSettings
		{
			Key = "test-key-test-key-test-key-test-key-0123456789",
			Issuer = "waao",
			Audience = "waao-frontend",
		};
		var jwt = new JwtIssuer(jwtSettings);

		var emailSender = new CapturingEmailSender();

		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["Auth:AdminEmails:0"] = "higor@waao.com.br",
				["Frontend:BaseUrl"] = "https://fe.test",
			})
			.Build();

		var service = new AuthService(
			db,
			jwt,
			new StreakTracker(db),
			new BadgeEvaluator(db),
			new RegisterValidator(),
			new LoginValidator(),
			new ChangePasswordValidator(),
			emailSender,
			new VerifyEmailValidator(),
			new ResendVerificationValidator(),
			config,
			NullLogger<AuthService>.Instance);

		return new AuthServiceFactory(service, db, emailSender);
	}
}
