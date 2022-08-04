using Microsoft.Extensions.DependencyInjection;

namespace DanmakuR.Connection;

public abstract class WrappedService<TService>
{
	public abstract TService GetRequiredService();
	public abstract TService? GetService();
}

public class WrappedService<TService, TImpl> : WrappedService<TService> where TImpl : notnull, TService
{
	private readonly IServiceProvider sp;

	public WrappedService(IServiceProvider sp)
	{
		this.sp = sp;
	}

	public override TService? GetService()
	{
		return sp.GetService<TImpl>();
	}

	public override TService GetRequiredService()
	{
		return sp.GetRequiredService<TImpl>();
	}
}

public class WrappedInstance<TService> : WrappedService<TService>
	where TService : notnull
{
	private readonly object impl;
	public WrappedInstance(object impl)
	{
		this.impl = impl;
	}

	public override TService GetRequiredService()
	{
		return (TService)impl;
	}

	public override TService? GetService()
	{
		return GetRequiredService();
	}
}

public static class WrappingServicesExtensions
{
	private static readonly Type wrapperType = typeof(WrappedService<>);
	private static readonly Type wrapperImplType = typeof(WrappedService<,>);
	public static IServiceCollection Wrap(this IServiceCollection services, Type beingWrappedType)
	{
		foreach (var sd in services.Take(services.Count))
		{
			if (sd.ServiceType == beingWrappedType)
			{
				var newServiceType = wrapperType.MakeGenericType(beingWrappedType);

				if (sd.ImplementationType != null)
				{
					var newImplType = wrapperImplType.MakeGenericType(beingWrappedType, sd.ImplementationType);
					services.Add(new ServiceDescriptor(newServiceType, newImplType, sd.Lifetime));
				}
				else if (sd.ImplementationInstance != null)
				{
					services.AddSingleton(newServiceType, Activator.CreateInstance(newServiceType, sd.ImplementationInstance)!);
				}
				else if (sd.ImplementationFactory != null)
				{
					services.Add(new ServiceDescriptor(
						newServiceType, (IServiceProvider sp) =>
						Activator.CreateInstance(newServiceType, sd.ImplementationFactory(sp))!,
						sd.Lifetime
					));
				}
			}
		}

		return services;
	}

	public static IServiceCollection Wrap<T>(this IServiceCollection services)
	{
		return Wrap(services, typeof(T));
	}
}
