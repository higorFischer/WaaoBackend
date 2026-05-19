namespace Waao.Services.Abstractions;

public sealed class EmailNotVerifiedException(string email)
	: Exception($"Email '{email}' is not verified.")
{
	public string Email { get; } = email;
}
