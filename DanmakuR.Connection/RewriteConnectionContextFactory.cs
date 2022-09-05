using DanmakuR.HandshakeProxy;
using DanmakuR.Protocol;
using DanmakuR.Protocol.Model;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.SignalR.Protocol;
using Microsoft.Extensions.Options;
using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;

namespace DanmakuR.Connection
{
	public class RewriteConnectionContextFactory : IConnectionFactory
	{
		private readonly IHandshakeProtocol protocol;
		private readonly Handshake2 handshake;
		private readonly BLiveOptions protocol_options;

		private List<EndPoint>? endpoints;
		private int next_endpoint = 0;

		private IConnectionFactory basefac;

		public RewriteConnectionContextFactory(IHandshakeProtocol protocol, WrappedService<IConnectionFactory> service, IOptions<BLiveOptions> options)
		{
			protocol_options = options.Value;
			handshake = protocol_options.Handshake;
			basefac = service.GetRequiredService();
			this.protocol = protocol;
		}

		private static IEnumerable<EndPoint> BuildWsEndPoints(Host[] hosts)
		{
			var builder = new UriBuilder(Uri.UriSchemeWs);
			foreach (Host host in hosts)
			{
				builder.Host = host.host;
				builder.Port = host.ws_port;
				yield return new UriEndPoint(builder.Uri);
			}
		}

		private static IEnumerable<EndPoint> BuildWssEndPoints(Host[] hosts)
		{
			var builder = new UriBuilder(Uri.UriSchemeWss);
			foreach (Host host in hosts)
			{
				builder.Host = host.host;
				builder.Port = host.wss_port;
				yield return new UriEndPoint(builder.Uri);
			}
		}

		private static async ValueTask<List<EndPoint>> BuildIpEndPointsAsync(Host[] hosts, CancellationToken cancellationToken)
		{
			List<EndPoint> endpoints = new(hosts.Length);

			// 没有ToListAsync，干脆包两层吧
			await foreach (var ent in BuildListAsync())
				endpoints.Add(ent);

			return endpoints;

			async IAsyncEnumerable<IPEndPoint> BuildListAsync()
			{
				foreach (Host host in hosts)
				{
					IPAddress[] iplist = await Dns.GetHostAddressesAsync(host.host, cancellationToken);
					foreach (var ip in iplist)
					{
						yield return new IPEndPoint(ip, host.port);
					}
				}
			}
		}

		private EndPoint SelectEndpoint()
		{
			Debug.Assert(endpoints != null);
			return endpoints[Random.Shared.Next(endpoints.Count)];
		}

		public async ValueTask<ConnectionContext> ConnectAsync(EndPoint endpoint, CancellationToken cancellationToken = default)
		{
			ConnectionContext ctx;
			string? connectionId = null;
			if (endpoint is PlaceHoldingEndPoint)
			{
				if (endpoints == null)
				{
					Host[]? hosts = null;

					using HttpClient httpClient = new();


					var negotiateResponse = await httpClient.GetFromJsonAsync<ControllerResponse<DanmuInfoData>>(
							$"https://api.live.bilibili.com/xlive/web-room/v1/index/getDanmuInfo?id={handshake.Roomid}",
							NegotiateContext.Default.Options,
							cancellationToken);

					if (negotiateResponse != null && negotiateResponse.IsValid)
					{
						hosts = negotiateResponse.data.host_list;
						handshake.CdnToken = negotiateResponse.data.token;
						connectionId = negotiateResponse.data.token;
					}

					hosts ??= Host.DefaultHosts;

					endpoints = protocol_options.TransportType switch
					{
						TransportTypes.SecureWebsocket => BuildWssEndPoints(hosts).ToList(),
						TransportTypes.RawSocket => await BuildIpEndPointsAsync(hosts, cancellationToken),
						_ => BuildWsEndPoints(hosts).ToList(),
					};
				}

				endpoint = SelectEndpoint();
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
					if (HandshakeProtocol.TryParseRequestMessage(ref buffer, out var req))
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
			if (connectionId != null)
			{
				ctxRewrite.ConnectionId = connectionId;
				ctx.ConnectionId = connectionId;
			}

			return ctxRewrite;
		}
	}
}
