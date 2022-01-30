using System.Buffers;
using System.Text.Json;
using Microsoft.AspNetCore.SignalR;

namespace DanmakuR
{
	/// <summary>
	/// 接收信息
	/// </summary>
	/// <remarks>绑到<see cref="Hub{IDanmakuSource}"/>上</remarks>
	public interface IDanmakuSource
	{
		// public void OnMessage(JsonEncodedText message);
		public Task OnPopularity(int popularity);
	}
}
