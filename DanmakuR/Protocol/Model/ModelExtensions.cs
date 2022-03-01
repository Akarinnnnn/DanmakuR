namespace DanmakuR.Protocol.Model;

public static class ModelExtensions
{
	public static Handshake3 SetCdnKey(this Handshake3 req ,string token)
	{
		req.AdditionalAuthParams.TryAdd("key", token);
		return req;
	}
}