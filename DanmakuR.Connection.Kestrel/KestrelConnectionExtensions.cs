using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Server.Kestrel.Transport.Sockets;

namespace DanmakuR.Connection.Kestrel;

public static class KestrelConnectionExtensions
{
	private const string KestrelSocketConnectionFactory = "Microsoft.AspNetCore.Server.Kestrel.Transport.Sockets.SocketConnectionFactory";
	private static readonly Type? FactoryType = Type.GetType(KestrelSocketConnectionFactory);
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
