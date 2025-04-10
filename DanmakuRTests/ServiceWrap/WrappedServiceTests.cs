using DanmakuR.Connection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;

namespace DanmakuRTests.ServiceWrap;

internal interface IService { }
internal interface IServiceInstance { }
internal interface IServiceFactory { }

internal class ServiceConstructor : IService
{

}

internal class ServiceInstance : IServiceInstance
{

}


internal class ServiceFactory : IServiceFactory
{

}

[TestClass]
public class WrappedServiceTests
{
	private ServiceCollection services = new();

	[TestMethod]
	public void TestWrappingConstructorBasedService()
	{
		services.AddSingleton<IService, ServiceConstructor>();
		services.Wrap<IService>();
		var sp = services.BuildServiceProvider();

		var serviceByOriginal = sp.GetRequiredService<IService>();
		var wrapped = sp.GetRequiredService<WrappedService<IService>>();

		Assert.IsNotNull(wrapped);
		Assert.IsNotNull(serviceByOriginal);

		Assert.AreSame(serviceByOriginal, wrapped.GetRequiredService());
	}

	[TestMethod]
	public void TestWrappingInstanceBasedService()
	{
		services.AddSingleton<IServiceInstance>(new ServiceInstance());
		services.Wrap<IServiceInstance>();
		var sp = services.BuildServiceProvider();

		var serviceByOriginal = sp.GetRequiredService<IServiceInstance>();
		var wrapped = sp.GetRequiredService<WrappedService<IServiceInstance>>();

		Assert.IsNotNull(wrapped);
		Assert.IsNotNull(serviceByOriginal);

		Assert.AreSame(serviceByOriginal, wrapped.GetRequiredService());
	}

	[TestMethod]
	public void TestWrappingFactoryBasedService()
	{
		services.AddSingleton<IServiceFactory>((IServiceProvider sp) => new ServiceFactory());
		services.Wrap<IServiceFactory>();
		var sp = services.BuildServiceProvider();

		var serviceByOriginal = sp.GetRequiredService<IServiceFactory>();
		var wrapped = sp.GetRequiredService<WrappedService<IServiceFactory>>();

		Assert.IsNotNull(wrapped);
		Assert.IsNotNull(serviceByOriginal);

		Assert.AreSame(serviceByOriginal, wrapped.GetRequiredService());
	}
}
