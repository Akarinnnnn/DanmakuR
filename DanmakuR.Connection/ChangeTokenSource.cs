using DanmakuR.Protocol;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;

namespace DanmakuR.Connection;

/// <summary>
/// 不应直接使用
/// </summary>
public class ChangeTokenSource : IOptionsChangeTokenSource<BLiveOptions>
{
	private volatile ConfigurationReloadToken reloadToken = new();

	public ChangeTokenSource(string? name)
	{
		Name = name ?? Options.DefaultName;
	}

	internal void Changed()
	{
		var oldReloadToken = Interlocked.Exchange(ref reloadToken, new ConfigurationReloadToken());
		oldReloadToken.OnReload();
	}

	public string? Name { get; }

	public IChangeToken GetChangeToken()
	{
		return reloadToken;
	}
}
