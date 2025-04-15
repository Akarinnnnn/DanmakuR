using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics.CodeAnalysis;

namespace DanmakuR.Connection;

[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods)]
public abstract class WrappedService<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods)] TService>
{
	public abstract TService GetRequiredService();
	public abstract TService? GetService();
}

[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods)]
public class WrappedService<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods)] TService, 
	[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods)] TImpl>
	: WrappedService<TService> where TImpl : notnull, TService
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

[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicMethods)]
public class WrappedInstance<TService>([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicMethods)]
	object impl) : WrappedService<TService>
	where TService : notnull
{
	private readonly object impl = impl;

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
	private static readonly Type abstractWrapperType = typeof(WrappedService<>);
	private static readonly Type wrappedConstructorServiceType = typeof(WrappedService<,>);
	private static readonly Type wrappedInstanceServiceType = typeof(WrappedInstance<>);

	public static IServiceCollection Wrap(this IServiceCollection services, Type beingWrappedType)
	{
		var newServiceType = abstractWrapperType.MakeGenericType(beingWrappedType);

		for (int i = 0; i < services.Count; i++)
		{
			ServiceDescriptor sd = services[i];
			if (sd.ServiceType == newServiceType)
				// 已经包装过，本方案无法处理高阶代理
				throw new InvalidOperationException($"类型{beingWrappedType.Name}已经被包装一次，无法继续包装本类型");

			if (sd.ServiceType == beingWrappedType)
			{

				if (sd.ImplementationType != null)
				{
					var newImplType = wrappedConstructorServiceType.MakeGenericType(beingWrappedType, sd.ImplementationType);
					services[i] = new ServiceDescriptor(newServiceType, newImplType, sd.Lifetime);
					services.Add(new ServiceDescriptor(sd.ServiceType, (IServiceProvider sp) => sp.GetRequiredService(sd.ImplementationType), sd.Lifetime));
					services.Add(new ServiceDescriptor(sd.ImplementationType, sd.ImplementationType, sd.Lifetime));
				}
				else if (sd.ImplementationInstance != null)
				{
					Type wrappedInstanceType = wrappedInstanceServiceType.MakeGenericType(sd.ServiceType);
					services.AddSingleton(newServiceType, Activator.CreateInstance(wrappedInstanceType, sd.ImplementationInstance)!);
				}
				else if (sd.ImplementationFactory != null)
				{
					Type wrappedInstanceType = wrappedInstanceServiceType.MakeGenericType(sd.ServiceType);
					services.Add(new ServiceDescriptor(
						newServiceType, 
						sp => Activator.CreateInstance(wrappedInstanceType, sp.GetRequiredService(sd.ServiceType))!,
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
