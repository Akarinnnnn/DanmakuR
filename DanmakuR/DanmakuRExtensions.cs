using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using DanmakuR.Protocol;
using DanmakuR.Protocol.Model;
using Microsoft.AspNetCore.SignalR.Protocol;
using Microsoft.AspNetCore.SignalR.Client;

namespace DanmakuR
{
	public static class DanmakuRExtensions
	{
		public static IHubConnectionBuilder UseBDanmakuProtocol(this IHubConnectionBuilder builder)
		{
			builder.Services.RemoveAll<IHubProtocol>()
				.AddSingleton<BDanmakuProtocol>()
				;//.Configure();
			
			return builder;
		}

		public static IHubConnectionBuilder ConfigureHandshake(this IHubConnectionBuilder builder, Action<Handshake2> configure)
		{
			builder.Services.AddOptions<Handshake2>()
				.Configure(configure)
				.PostConfigure(opt => opt.EnsureValid());

			return builder;
		}

		public static IHubConnectionBuilder ConfigureHandshake3(this IHubConnectionBuilder builder, Action<Handshake3> action)
		{
			builder.Services.AddOptions<Handshake3>()
				.Configure(action)
				.PostConfigure(opt => opt.EnsureValid());

			return builder;
		}

		// public static async ValueTask<IHubConnectionBuilder> 
	}
}