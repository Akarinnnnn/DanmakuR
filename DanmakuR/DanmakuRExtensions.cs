using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.AspNetCore.SignalR.Protocol;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.Http.Connections.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.SignalR;

using DanmakuR.Protocol;
using DanmakuR.Protocol.Model;

namespace DanmakuR
{
	public static class DanmakuRExtensions
	{
		public static IHubConnectionBuilder UseWebsocketProtocol(this IHubConnectionBuilder builder, Action<HttpConnectionOptions>? configureHttpConnection = null)
		{
			
			builder.Services.AddSingleton<IHubProtocol, BDanmakuProtocol>();
			builder.WithUrl("ws://broadcastlv.chat.bilibili.com:2244/sub", (opt) =>
			{
				configureHttpConnection?.Invoke(opt);

				opt.SkipNegotiation = true;
				opt.Transports = HttpTransportType.WebSockets;
			});
			return builder;
		}

		public static IHubConnectionBuilder ConfigureHandshake(this IHubConnectionBuilder builder, Action<Handshake2> action)
		{
			builder.Services.AddOptions<Handshake2>()
				.Configure(action)
				.PostConfigure(opt => opt.EnsureValid());

			return builder;
		}
	}
}