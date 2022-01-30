using System.Text.Json.Serialization;
using Microsoft.AspNetCore.SignalR.Client;
namespace DanmakuR.Protocol.Model
{
	/// <summary>
	/// 
	/// </summary>
	/// <remarks>
	/// 推荐使用<see cref="DanmakuRExtensions.ConfigureHandshake(IHubConnectionBuilder, Action{Handshake2})"/>
	/// </remarks>
	[JsonNumberHandling(JsonNumberHandling.Strict)]
	public class Handshake2
	{
		/// <summary>
		/// 房间号
		/// </summary>
		public int Roomid;
		/// <summary>
		/// 客户端版本
		/// </summary>
		public string? Clientver = null;
		/// <summary>
		/// 平台名称
		/// </summary>
		public string? Platform = null;
		/// <summary>
		/// 数据包协议版本，为1或2
		/// </summary>
		/// <remarks>1不使用压缩，2使用压缩</remarks>
		public int? Protover = 2;
		/// <summary>
		/// 用户id
		/// </summary>
		public int? Uid = null;
		/// <summary>
		/// 不知道啥，总之写2
		/// </summary>
		public int? Type = null;

		/// <summary>
		/// 确保值有效
		/// </summary>
		/// <remarks>将无效<see cref="type"/>改成2，<see cref="protover"/>改成2</remarks>
		public void EnsureValid()
		{
			if (Type != null && Type != 2)
				Type = 2;
			if (Protover != null && Protover != 2 && Protover != 1)
				Protover = 2;
		}
	}
}
