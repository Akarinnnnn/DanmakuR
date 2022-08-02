using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using Microsoft.AspNetCore.SignalR.Protocol;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.AspNetCore.Connections;

using DanmakuR.Connection;
using DanmakuR.Protocol;
using DanmakuR.Protocol.Model;
using Microsoft.AspNetCore.Server.Kestrel.Transport.Sockets;
using System.Net;
using Microsoft.AspNetCore.Http.Connections.Client;
using System.Text.Json;

namespace DanmakuR
{
	public static class DanmakuRExtensions
	{
		public static IHubConnectionBuilder PrepareForBLiveProtocol(this IHubConnectionBuilder builder,
			TransportTypes transportType = TransportTypes.RawSocket,
			Action<BLiveOptions>? configureOptions = null)
		{
			builder.Services.RemoveAll<IHubProtocol>()
				.AddBLiveProtocol()
				.RemoveAll<IConnectionFactory>()
				.AddSingleton<IConnectionFactory, RewriteConnectionContextFactory>()
				.AddSingleton<IHandshakeProtocol, BLiveHandshakeProtocol>()
				.AddLogging()
				.AddHandshake2()
				.AddSingleton<EndPoint, PlaceHoldingEndPoint>()
				.AddBLiveOptions(transportType, configureOptions);

			return builder;
		}

		public static IHubConnectionBuilder WithRoomid(this IHubConnectionBuilder builder, int roomid, Action<Handshake2>? configure = null)
		{
			builder.Services.AddHandshake2();
			builder.Services.Configure<Handshake2>(x =>
			{
				x.Roomid = roomid;
				configure?.Invoke(x);
			});
			return builder;
		}

		public static IHubConnectionBuilder UseSocketTransport(this IHubConnectionBuilder builder, 
			Action<SocketConnectionFactoryOptions> configureOption)
		{
			builder.Services.Configure<BLiveOptions>(opt => opt.TransportType = TransportTypes.RawSocket)
				.AddOptions<SocketConnectionFactoryOptions>()
				.Configure(configureOption);
			return builder;
		}

		public static IHubConnectionBuilder UseWebsocketTransport(this IHubConnectionBuilder builder,
			Action<HttpConnectionOptions> configureOptions,
			bool isSecure)
		{
			builder.Services.Configure<BLiveOptions>(opt => opt.TransportType = isSecure
				? TransportTypes.SecureWebsocket
				: TransportTypes.InsecureWebsocket)
				.AddOptions<HttpConnectionOptions>()
				.Configure(configureOptions);

			return builder;
		}

		public static HubConnection BindListeners(this HubConnection connection, IDanmakuSource listener)
		{
			connection.On(WellKnownMethods.OnPopularity.Name, new Func<int, Task>(listener.OnPopularityAsync));
			connection.On(WellKnownMethods.OnMessageJsonDocument.Name, new Func<string, JsonDocument, Task>(listener.OnMessageJsonDocumentAsync));

			return connection;
		}
	}
}