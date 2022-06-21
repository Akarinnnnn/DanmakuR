global using System.Buffers;
global using Xunit;
using DanmakuR.Protocol.Model;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using System.Diagnostics.CodeAnalysis;

namespace DanmakuR.Protocol.Tests;

public class Startup
{
	[SuppressMessage("Performance", "CA1822:将成员标记为 static",
		Justification = "XUnit.DependencyInject限制")]
	public void ConfigureServices(IServiceCollection services)
	{
		services.AddSingleton<NullLoggerFactory>()
			.AddSingleton<NullLogger>()
			.AddHandshake2(2003470)
			.AddBLiveOptions()
			.AddBLiveProtocol();
	}
}
