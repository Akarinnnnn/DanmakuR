namespace DanmakuR.HandshakeProxy;

public class HandshakeProxyConnectionOptions
{
	public TransformData RewriteAppRequest { get; set; } = null!;
	public TransformData RewriteServerResponse { get; set; } = null!;
}
