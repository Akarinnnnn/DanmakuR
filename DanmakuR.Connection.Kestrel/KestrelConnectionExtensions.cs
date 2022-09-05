using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Server.Kestrel.Transport.Sockets;
using System.Reflection;

namespace DanmakuR.Connection.Kestrel;

public static class KestrelConnectionExtensions
{
	private const string AssemblyName = "Microsoft.AspNetCore.Server.Kestrel.Transport.Sockets.dll";
	private const string TypeName = "Microsoft.AspNetCore.Server.Kestrel.Transport.Sockets.SocketConnectionFactory";

	private static readonly Type? FactoryType;
	// private static readonly Assembly TargetAssembly = Assembly.LoadFrom(AssemblyName);

	static KestrelConnectionExtensions()
	{
		// var kestrelAssembly = Assembly.LoadFrom(AssemblyName);
		var kestrelAssembly = typeof(Microsoft.AspNetCore.Server.Kestrel.Transport.Sockets.SocketConnectionContextFactory).Assembly;
		FactoryType = kestrelAssembly.GetType(TypeName, throwOnError: false);
	}

	public static IServiceCollection AddKestrelClientSocket(this IServiceCollection services)
	{
		if (FactoryType == null)
		{
			throw new NotSupportedException("未加载Kestrel程序集");
		}

		services.AddSingleton(typeof(IConnectionFactory), FactoryType)
			.AddOptions<SocketTransportOptions>();
		
		return services;
	}
}
