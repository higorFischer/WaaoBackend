namespace Waao.Services.Abstractions;

public sealed class InvalidVerificationTokenException()
	: Exception("Invalid or expired verification token.");
