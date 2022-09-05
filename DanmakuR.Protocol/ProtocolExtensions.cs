using DanmakuR.Protocol.Model;
using Microsoft.AspNetCore.SignalR.Protocol;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;



namespace DanmakuR.Protocol;

/// <summary>
/// 读取服务器端发来的json，并解析为返回一个<see cref="object"/>
/// </summary>
/// <param name="reader"></param>
/// <returns>稍后传递给事先注册的函数</returns>
public delegate object CommandBinder(Utf8JsonReader reader);


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
			o.Handshake = hs2.Value;
		})
		.PostConfigure(o =>
		{
			// o.SerializerOptions ??= new(System.Text.Json.JsonSerializerDefaults.General);
			o.SerializerOptions.Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping;
			o.SerializerOptions.AddContext<HandshakeJsonContext>();
		})
		.Validate(o =>
		{
			return o.Handshake != null && o.SerializerOptions != null;
		});

		if (configure != null)
			builder.Configure(configure);

		return services;
	}
}
