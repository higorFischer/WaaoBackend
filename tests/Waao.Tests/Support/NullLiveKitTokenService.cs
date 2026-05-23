using Waao.Services.Abstractions.Dtos;
using Waao.Services.Abstractions.Services;

namespace Waao.Tests.Support;

public sealed class NullLiveKitTokenService : ILiveKitTokenService
{
	public static readonly NullLiveKitTokenService Instance = new();

	public string MintToken(LiveKitTokenRequest request) => "test-jwt-token";
	public string MintGuestToken(GuestLiveKitTokenRequest request) => "test-guest-jwt-token";
}
