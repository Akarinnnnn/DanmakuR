using System.Resources;

namespace DanmakuR.Resources;

internal static class SysSR
{
	private static ResourceManager? s_resourceManager;
	internal static ResourceManager ResourceManager => s_resourceManager ??=
		new ResourceManager(
			typeof(string).Assembly.GetType("System.Private.CoreLib.Strings") ??
			typeof(SysSR));

	private static string GetResourceString(string key) => ResourceManager.GetString(key)!;

	/// <summary>Enum value was out of legal range.</summary>
	internal static string @ArgumentOutOfRange_Enum => GetResourceString("ArgumentOutOfRange_Enum");
}
