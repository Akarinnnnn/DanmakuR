using DanmakuR.Protocol.Model;
using Microsoft.AspNetCore.SignalR.Protocol;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DanmakuR.Protocol
{
	public static class ProtocolExtensions
	{
		public static IServiceCollection AddBLiveProtocol(this IServiceCollection services)
		{
			services.TryAddSingleton<IHubProtocol, BLiveProtocol>();
			return services;
		}

		public static IServiceCollection AddHandshake2(this IServiceCollection services)
		{
			var builder = services.AddOptions<Handshake2>()
				.PostConfigure(hs2 => hs2.EnsureValid());

			return services;
		}

		public static IServiceCollection AddBLiveOptions(this IServiceCollection services,
			TransportTypes transportType = TransportTypes.RawSocket,
			Action<BLiveOptions>? configure = null)
		{
			var builder = services.AddOptions<BLiveOptions>()
			.Configure<IOptions<Handshake2>>((o, hs2) =>
			{
				o.TransportType = transportType;
				o.HandshakeSettings = hs2.Value;
			})
			.PostConfigure(o =>
			{
				if (o.SerializerOptions == null)
					o.SerializerOptions = new(System.Text.Json.JsonSerializerDefaults.General);
				o.SerializerOptions.Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping;
				o.SerializerOptions.AddContext<HandshakeJsonContext>();
			})
			.Validate(o =>
			{
				return o.HandshakeSettings != null && o.SerializerOptions != null;
			});

			if (configure != null)
				builder.Configure(configure);

			return services;
		}
	}
}
