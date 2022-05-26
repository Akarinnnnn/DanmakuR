using DanmakuR.Protocol;
using DanmakuR.Protocol.Model;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.Http.Connections.Client;
using Microsoft.AspNetCore.Server.Kestrel.Transport.Sockets;
using Microsoft.Extensions.Options;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;

namespace DanmakuR.Connection
{
	public class BLiveConnectionFactory : IConnectionFactory
	{
		private readonly SocketConnectionFactoryOptions? socket_options;
		private readonly HttpConnectionOptions? http_options;
		private readonly Handshake2 handshake;
		private readonly BDanmakuOptions protocol_options;
		private readonly ILoggerFactory logger_factory;
		public BLiveConnectionFactory(IOptions<SocketConnectionFactoryOptions>? socketOptions,
			IOptions<HttpConnectionOptions>? httpOptions,
			IOptions<BDanmakuOptions> bDanmakuOptions,
			ILoggerFactory loggerFactory)
		{
			if (socketOptions == null && httpOptions == null)
				throw new ArgumentException($"至少要注入{nameof(SocketConnectionFactoryOptions)}或{nameof(HttpConnectionOptions)}中的一个");

			protocol_options = bDanmakuOptions.Value;
			handshake = protocol_options.HandshakeSettings;
			logger_factory = loggerFactory;

			if (socketOptions != null)
				socket_options = socketOptions.Value;
			else
#pragma warning disable CS8602 // 解引用可能出现空引用。
				http_options = httpOptions.Value;
#pragma warning restore CS8602 // httpOptions绝不为null
		}

		public async ValueTask<ConnectionContext> ConnectAsync(EndPoint endpoint, CancellationToken cancellationToken = default)
		{
			using HttpClient httpClient = new();
			var negotiateResponse = await httpClient.GetFromJsonAsync<NegotiateResponse>(
				$"https://api.live.bilibili.com/xlive/web-room/v1/index/getDanmuInfo?id={handshake.Roomid}",
				NegotiateContext.Default.Options,
				cancellationToken);
			ConnectionContext context;
			Debug.Assert(socket_options == null && http_options == null, "");

			if (negotiateResponse != null && negotiateResponse.IsValid)
			{
				handshake.CdnToken = negotiateResponse.data.token;
			}

			if (socket_options != null)
			{
				context = await ConnectSocket(negotiateResponse, cancellationToken);
			}
			else if (http_options != null)
			{
				context = await ConnectWebsocket(negotiateResponse, cancellationToken);
			}
			else
			{
				throw new ArgumentNullException(null, "既没上http也没有socket，这东西怎么活到现在的？");
			}
			return context;
		}

		private async ValueTask<ConnectionContext> ConnectWebsocket(NegotiateResponse? negotiateResponse, CancellationToken cancellationToken)
		{
			SanitizeHttpOptions();
			UriBuilder builder = CreateUriBuilder();
			if (negotiateResponse != null && negotiateResponse.IsValid)
			{
				List<WebSocketException> exceptions = new(negotiateResponse.data.host_list.Length);
				HttpConnectionOptions options = http_options!;
				foreach (Host host in negotiateResponse.data.host_list)
				{
					try
					{
						builder.Port = protocol_options.TransportType switch
						{
							TransportTypes.Unspecified or TransportTypes.Websocket => host.ws_port,
							TransportTypes.SecureWebsocket => host.wss_port,
							_ => throw ExceptionForWsTransportMismatch()
						};
						builder.Host = host.host;
						options.Url = builder.Uri;
						var httpContext = new HttpConnection(options, logger_factory);
						await httpContext.StartAsync(cancellationToken);
						return httpContext;
					}
					catch (WebSocketException ex)
					{
						exceptions.Add(ex);
					}
				}

				throw new AggregateException("全部服务器均连接失败", exceptions);
			}
			else
			{
				builder.Host = Host.DefaultHosts[0].host;
				http_options!.Url = builder.Uri;
				var httpContext = new HttpConnection(http_options!, logger_factory);
				await httpContext.StartAsync(TransferFormat.Binary, cancellationToken);
				return httpContext;
			}

		}

		private UriBuilder CreateUriBuilder()
		{
			return new()
			{
				Scheme = protocol_options.TransportType switch
				{
					TransportTypes.Unspecified or TransportTypes.Websocket => "ws",
					TransportTypes.SecureWebsocket => "wss",
					_ => throw ExceptionForWsTransportMismatch()
				},
				Path = "sub"
			};
		}
		private static InvalidOperationException ExceptionForWsTransportMismatch()
		{
			return new InvalidOperationException(
				$"提供了{nameof(HttpConnectionOptions)}，" +
				$"但传输方式不是{nameof(TransportTypes.Websocket)}" +
				$"或{nameof(TransportTypes.SecureWebsocket)}"
				);
		}

		private async ValueTask<ConnectionContext> ConnectSocket(NegotiateResponse? negotiateResponse, CancellationToken cancellationToken)
		{
			Socket socket;
			if (negotiateResponse == null || !negotiateResponse.IsValid)
				socket = await CreateSocketAsync(Host.DefaultHosts, cancellationToken);
			else
				socket = await CreateSocketAsync(negotiateResponse.data.host_list, cancellationToken);
			var socketFactory = new SocketConnectionContextFactory(socket_options!,
								logger_factory.CreateLogger<SocketConnectionContextFactory>());

			return socketFactory.Create(socket);
		}

		private static async ValueTask<Socket> CreateSocketAsync(Host[] hosts, CancellationToken cancellationToken)
		{
			Socket socket = new(SocketType.Seqpacket, ProtocolType.Tcp);
			List<SocketException> exceptions = new(hosts.Length);
			foreach (Host host in hosts)
			{
				try
				{
					await socket.ConnectAsync(host.host, host.port, cancellationToken);
					return socket;
				}
				catch (SocketException ex)
				{
					exceptions.Add(ex);
					continue;
				}
			}
			throw new AggregateException("全部服务器均连接失败", exceptions);
		}

		private void SanitizeHttpOptions()
		{
			var localOpt = http_options!;
			localOpt.SkipNegotiation = true;
			localOpt.Transports = HttpTransportType.WebSockets;
			localOpt.AccessTokenProvider = null;
		}
	}
}
