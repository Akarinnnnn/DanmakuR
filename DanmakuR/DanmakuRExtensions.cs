using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using Microsoft.AspNetCore.SignalR.Protocol;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.AspNetCore.Connections;

using DanmakuR.Protocol;
using DanmakuR.Protocol.Model;
using Microsoft.AspNetCore.Server.Kestrel.Transport.Sockets;
using System.Net;

namespace DanmakuR
{
	public static class DanmakuRExtensions
	{
		private const string KerstrelFactory = "Microsoft.AspNetCore.Server.Kestrel.Transport.Sockets.SocketConnectionFactory";

		public static IHubConnectionBuilder UseBDanmakuProtocol(this IHubConnectionBuilder builder)
		{
			builder.Services.RemoveAll<IHubProtocol>()
				.AddSingleton<IHubProtocol, BDanmakuProtocol>()
				;//.Configure();
			
			return builder;
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="builder"></param>
		public static IHubConnectionBuilder UseRawSocketTransport(this IHubConnectionBuilder builder)
		{
			Type kestrelFactory = typeof(SocketTransportFactory).Assembly.GetType(KerstrelFactory, true, false)!;
			builder.Services.AddSingleton(kestrelFactory, typeof(IConnectionFactory));
			return builder;
		}

		public static IHubConnectionBuilder ConfigureSocketOptions(this IHubConnectionBuilder builder, Action<SocketConnectionFactoryOptions>? configure)
		{
			builder.Services.AddOptions<SocketConnectionFactoryOptions>()
				.Configure(configure);
			return builder;
		}

		public static IHubConnectionBuilder ConfigureHandshake(this IHubConnectionBuilder builder, Action<Handshake2> configure)
		{
			builder.Services.AddOptions<Handshake2>()
				.Configure(configure)
				.PostConfigure(opt => opt.EnsureValid());

			return builder;
		}

		public static IHubConnectionBuilder ConfigureHandshake3(this IHubConnectionBuilder builder, Action<Handshake3> configure)
		{
			builder.Services.AddOptions<Handshake3>()
				.Configure(configure)
				.PostConfigure(opt => opt.EnsureValid());

			return builder;
		}

		/// <summary>
		/// 异步添加Socket传输实现，并选择第一个socket服务器，同时设置房号
		/// </summary>
		/// <param name="builder"></param>
		/// <param name="roomid">房间号</param>
		/// <param name="cancellationToken"></param>
		/// <returns></returns>
		/// <exception cref="System.Net.Sockets.SocketException">DNS解析失败</exception>
		public static async ValueTask<IHubConnectionBuilder> WithSocketEndpointAsync(this IHubConnectionBuilder builder, 
			int roomid, CancellationToken cancellationToken = default)
		{
			string negotiateEndpoint = $"https://api.live.bilibili.com/xlive/web-room/v1/index/getDanmuInfo?id={roomid}";
			using HttpClient client = new ();
			var result = await client.GetFromJsonAsync<Negotiate>(negotiateEndpoint, NegotiateContext.Default.Options, cancellationToken);
			string endpoint;
			short port;
			if (result == null || !result.IsValid)
			{ 
				endpoint = "broadcastlv.chat.bilibili.com";
				port = 2243;
			}
			else
			{
				var chosen = result.data.host_list[0];
				endpoint = chosen.host;
				port = chosen.port;
			}
			IPAddress endpointip = (await Dns.GetHostAddressesAsync(endpoint, cancellationToken))[0];

			builder.Services.AddSingleton<EndPoint, IPEndPoint>((_) => new IPEndPoint(endpointip, port))
			.Configure<Handshake2>((opt) => opt.Roomid = roomid);
			builder.UseRawSocketTransport();
			return builder;
		}
	}
}