using DanmakuR.Protocol;
using DanmakuR.Protocol.Model;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.Http.Connections.Client;
using Microsoft.AspNetCore.Server.Kestrel.Transport.Sockets;
using Microsoft.Extensions.Options;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;

namespace DanmakuR.Connection
{
	public class BLiveConnectionFactory : IConnectionFactory, IDisposable
	{
		private readonly SocketConnectionFactoryOptions? socket_options;
		private readonly HttpConnectionOptions? http_options;
		private readonly Handshake2 handshake;
		private readonly BLiveOptions protocol_options;
		private readonly ILoggerFactory logger_factory;
		
		private SocketConnectionContextFactory? socket_factory;
		private bool is_disposed;

		public BLiveConnectionFactory(IOptions<SocketConnectionFactoryOptions>? socketOptions,
			IOptions<HttpConnectionOptions>? httpOptions,
			IOptions<BLiveOptions> bDanmakuOptions,
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


		public virtual async ValueTask<ConnectionContext> ConnectAsync(EndPoint ep, CancellationToken cancellationToken = default)
		{
			using HttpClient httpClient = new();
			var negotiateResponse = await httpClient.GetFromJsonAsync<NegotiateResponse>(
				$"https://api.live.bilibili.com/xlive/web-room/v1/index/getDanmuInfo?id={handshake.Roomid}",
				NegotiateContext.Default.Options,
				cancellationToken);
			ConnectionContext context;
			Debug.Assert(socket_options != null || http_options != null, "");

			if (negotiateResponse != null && negotiateResponse.IsValid)
			{
				handshake.CdnToken = negotiateResponse.data.token;
			}

			if(protocol_options.TransportType == TransportTypes.Unspecified)
				protocol_options.TransportType = socket_options != null ?
					TransportTypes.RawSocket :
					TransportTypes.InsecureWebsocket;

			if (socket_options != null)
			{
				if(ep is IPEndPoint)
				{
					var socket = CreateSocket();
					await socket.ConnectAsync(ep);
					context = CreateSocketContext(socket);
				}
				else
				{
					context = await ConnectSocket(negotiateResponse, cancellationToken);
				}
			}
			else if (http_options != null)
			{
				context = await ConnectWebsocket(negotiateResponse, cancellationToken);
			}
			else
			{
				throw new ArgumentNullException($"{nameof(socket_options)}和{nameof(http_options)}", "这不科学");
			}

			if(negotiateResponse != null)
				context.ConnectionId = negotiateResponse.data!.token;

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
				Scheme = (protocol_options.TransportType & TransportTypes.Websocket) switch
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
			Socket socket = CreateSocket();

			if (negotiateResponse == null || !negotiateResponse.IsValid)
				await ConnectSocketAsync(Host.DefaultHosts, socket, cancellationToken);
			else
				await ConnectSocketAsync(negotiateResponse.data.host_list, socket, cancellationToken);

			return CreateSocketContext(socket);
		}

		private ConnectionContext CreateSocketContext(Socket socket)
		{
			socket_factory = new SocketConnectionContextFactory(
				socket_options!,
				logger_factory.CreateLogger<SocketConnectionContextFactory>()
			);

			return socket_factory.Create(socket);
		}

		private static Socket CreateSocket()
		{
			return new(SocketType.Stream, ProtocolType.Tcp)
			{
				Blocking = false,
				ReceiveTimeout = 45 * 1000
			};
		}

		private static async ValueTask ConnectSocketAsync(Host[] hosts, Socket socket, CancellationToken cancellationToken)
		{
			List<SocketException> exceptions = new(hosts.Length);
			foreach (Host host in hosts)
			{
				try
				{
					await socket.ConnectAsync(host.host, host.port, cancellationToken);
					return;
				}
				catch (SocketException ex)
				{
					exceptions.Add(ex);
					continue;
				}
			}
			throw new AggregateException("全部服务器均连接失败", exceptions.ToArray());
		}

		private void SanitizeHttpOptions()
		{
			var localOpt = http_options!;
			localOpt.SkipNegotiation = true;
			localOpt.Transports = HttpTransportType.WebSockets;
			localOpt.AccessTokenProvider = null;
		}

		protected virtual void Dispose(bool disposing)
		{
			if (!is_disposed)
			{
				if (disposing)
				{
					socket_factory?.Dispose();
				}

				is_disposed = true;
			}
		}

		public void Dispose()
		{
			Dispose(disposing: true);
			GC.SuppressFinalize(this);
		}
	}
}
