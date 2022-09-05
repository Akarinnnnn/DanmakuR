

using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Server.Kestrel.Transport.Sockets;
using Microsoft.Extensions.Options;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;

namespace DanmakuR.Connection.Kestrel;

public class KestrelConnectionFactory : IDisposable
{
	private readonly SocketConnectionContextFactory factory;
	public KestrelConnectionFactory(IOptions<SocketConnectionFactoryOptions> opt, ILoggerFactory loggerFactory)
	{
		ArgumentNullException.ThrowIfNull(opt);

		factory = new SocketConnectionContextFactory(opt.Value, loggerFactory.CreateLogger<SocketConnectionContextFactory>());
	}

	private static Socket CreateSocket()
	{
		return new(SocketType.Stream, ProtocolType.Tcp)
		{
			Blocking = false,
			ReceiveTimeout = 45 * 1000
		};
	}

	// public asy

	public async ValueTask<ConnectionContext> ConnectEndPointsAsync(EndPoint[] endpoint, CancellationToken cancellationToken = default)
	{
		Socket s = CreateSocket();
		List<Exception>? exlist = null;
		foreach (EndPoint ep in endpoint)
		{
			try
			{
				switch (ep)
				{
					case IPEndPoint:
						await s.ConnectAsync(ep, cancellationToken);
						break;
					case DnsEndPoint dns:
						if (Environment.OSVersion.Platform != PlatformID.Win32NT)
						{
							var ipentry = await Dns.GetHostEntryAsync(dns.Host, cancellationToken);
							await s.ConnectAsync(ipentry.AddressList, dns.Port, cancellationToken); 
						}
						else
						{
							await s.ConnectAsync(dns);
						}
						break;
				}
				return factory.Create(s);
			}
			catch (Exception e)
			{
				exlist ??= new List<Exception>();
				exlist.Add(e);
			}
		}


		throw new AggregateException("全部服务器都连接失败", exlist!);
	}

	public void Dispose()
	{
		factory.Dispose();
	}
}
