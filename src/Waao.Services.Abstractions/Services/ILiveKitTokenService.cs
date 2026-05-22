using Waao.Services.Abstractions.Dtos;

namespace Waao.Services.Abstractions.Services;

public interface ILiveKitTokenService
{
	/// <summary>Mints a LiveKit HS256 access token granting the participant join rights to a room.</summary>
	string MintToken(LiveKitTokenRequest request);
}
