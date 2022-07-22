using DanmakuR.HandshakeProxy;
using DanmakuR.Protocol;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Http.Connections.Client;
using Microsoft.AspNetCore.Server.Kestrel.Transport.Sockets;
using Microsoft.AspNetCore.SignalR.Protocol;
using Microsoft.Extensions.Options;
using System.Net;

namespace DanmakuR.Connection
{
	public class HandshakeProxiedConnectionFactory : BLiveConnectionFactory
	{
		private readonly IHandshakeProtocol protocol;

		public HandshakeProxiedConnectionFactory(IOptions<SocketConnectionFactoryOptions>? socketOptions, 
			IOptions<HttpConnectionOptions>? httpOptions, 
			IOptions<BLiveOptions> bDanmakuOptions,
			IHandshakeProtocol protocol,
			ILoggerFactory loggerFactory) 
			: base(socketOptions, httpOptions, bDanmakuOptions, loggerFactory)
		{
			this.protocol = protocol;
		}

		public override async ValueTask<ConnectionContext> ConnectAsync(EndPoint endpoint, CancellationToken cancellationToken = default)
		{
			var ctx = await base.ConnectAsync(endpoint, cancellationToken);
			var opts = new HandshakeProxyConnectionOptions
			{
				TransformResponse = (buffer, output) =>
				{
					if (protocol.TryParseResponseMessage(ref buffer, out var rsp))
					{
						HandshakeProtocol.WriteResponseMessage(rsp, output);
						return (true, buffer.Start);
					}
					else
					{
						return (false, buffer.Start);
					}
				},
				TransformRequest = (buffer, output) =>
				{
					if(HandshakeProtocol.TryParseRequestMessage(ref buffer, out var req))
					{
						protocol.WriteRequestMessage(req, output);
						return (true, buffer.Start);
					}
					else
					{
						return (false, buffer.Start);
					}
				}
			};
			var ctxRewrite = new RewriteHandshakeConnection(ctx, opts);
			ctxRewrite.Start();
			return ctxRewrite;
		}
	}
}
