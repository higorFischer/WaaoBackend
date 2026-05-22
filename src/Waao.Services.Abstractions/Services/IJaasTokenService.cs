using Waao.Services.Abstractions.Dtos;

namespace Waao.Services.Abstractions.Services;

public interface IJaasTokenService
{
	/// <summary>
	/// Mints a short-lived RS256-signed JaaS JWT for the given request.
	/// </summary>
	string MintToken(JaasTokenRequest request);
}
