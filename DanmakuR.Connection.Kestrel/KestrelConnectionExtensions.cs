using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Server.Kestrel.Transport.Sockets;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace DanmakuR.Connection.Kestrel;

public static class KestrelConnectionExtensions
{
	private const string TypeName = "Microsoft.AspNetCore.Server.Kestrel.Transport.Sockets.SocketConnectionFactory";

	private static readonly Type? FactoryType;
	// private static readonly Assembly TargetAssembly = Assembly.LoadFrom(AssemblyName);

	static KestrelConnectionExtensions()
	{
		// var kestrelAssembly = Assembly.LoadFrom(AssemblyName);
		var kestrelAssembly = typeof(SocketConnectionContextFactory).Assembly;
		FactoryType = kestrelAssembly.GetType(TypeName, throwOnError: false);
	}

	public static IServiceCollection AddKestrelClientSocket(this IServiceCollection services, ServiceLifetime lifetime)
	{
		if (FactoryType == null)
		{
			throw new NotSupportedException("未加载Kestrel程序集");
		}

		services.TryAdd(new ServiceDescriptor(typeof(IConnectionFactory), FactoryType, lifetime));
		services.AddOptions<SocketTransportOptions>();

		return services;
	}

	public static IHubConnectionBuilder UseSocketTransport(this IHubConnectionBuilder builder)
	{
		builder.Services.AddKestrelClientSocket(ServiceLifetime.Singleton);
		return builder;
	}
}
