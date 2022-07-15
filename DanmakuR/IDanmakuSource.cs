using System.Buffers;
using System.Text.Json;
using Microsoft.AspNetCore.SignalR.Client;

namespace DanmakuR
{
	/// <summary>
	/// 接收信息
	/// </summary>
	/// <remarks>绑到<see cref="HubConnection"/>上</remarks>
	public interface IDanmakuSource
	{
		// public void OnMessage(JsonEncodedText message);
		public Task OnPopularityAsync(int popularity);
		public Task OnMessageJsonDocumentAsync(string messageName, JsonDocument message);
		// public void OnMessage(string messageName, Utf8JsonReader reader);
	}
}
