using DanmakuR.HandshakeProxy;
using DanmakuR.Protocol;
using DanmakuR.Protocol.Model;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Http.Connections.Client;
//using Microsoft.AspNetCore.Server.Kestrel.Transport.Sockets;
using Microsoft.AspNetCore.SignalR.Protocol;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System.Net;
using System.Net.Http.Json;

namespace DanmakuR.Connection
{
	public class RewriteConnectionContextFactory : IConnectionFactory
	{
		private const string KestrelSocketConnectionFactory = "Microsoft.AspNetCore.Server.Kestrel.Transport.Sockets.SocketConnectionFactory";

		private readonly IHandshakeProtocol protocol;
		private readonly Handshake2 handshake;
		private readonly BLiveOptions protocol_options;

		private IConnectionFactory basefac;

		public RewriteConnectionContextFactory(IHandshakeProtocol protocol, WrappedService<IConnectionFactory> service)
		{

			basefac = service.GetRequiredService();
			this.protocol = protocol;
		}

		public async ValueTask<ConnectionContext> ConnectAsync(EndPoint endpoint, CancellationToken cancellationToken = default)
		{
			using HttpClient httpClient = new();
			var negotiateResponse = await httpClient.GetFromJsonAsync<NegotiateResponse>(
				$"https://api.live.bilibili.com/xlive/web-room/v1/index/getDanmuInfo?id={handshake.Roomid}",
				NegotiateContext.Default.Options,
				cancellationToken);
			ConnectionContext ctx;
			// Debug.Assert(socket_options != null || http_options != null, "");

			if (negotiateResponse != null && negotiateResponse.IsValid)
			{
				handshake.CdnToken = negotiateResponse.data.token;
			}


			ctx = await basefac.ConnectAsync(endpoint, cancellationToken);
			var opts = new HandshakeProxyConnectionOptions
			{
				RewriteServerResponse = (buffer, output) =>
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
				RewriteAppRequest = (buffer, output) =>
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
