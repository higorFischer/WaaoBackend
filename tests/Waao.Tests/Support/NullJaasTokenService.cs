using Waao.Services.Abstractions.Dtos;
using Waao.Services.Abstractions.Services;

namespace Waao.Tests.Support;

public sealed class NullJaasTokenService : IJaasTokenService
{
	public static readonly NullJaasTokenService Instance = new();

	public string MintToken(JaasTokenRequest request) => "test-jwt-token";
}
