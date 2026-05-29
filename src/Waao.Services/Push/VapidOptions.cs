namespace Waao.Services.Push;

public class VapidOptions
{
	public string Subject { get; set; } = string.Empty;
	public string PublicKey { get; set; } = string.Empty;
	public string PrivateKey { get; set; } = string.Empty;
}
