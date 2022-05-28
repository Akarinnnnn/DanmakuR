using System.Text.Json.Serialization;

namespace DanmakuR.Protocol.Model
{
	/// <summary>
	/// 
	/// </summary>
	/// <remarks>
	/// 推荐使用<see cref="DanmakuRExtensions.AddHandshake(IHubConnectionBuilder, Action{Handshake2})"/>
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
		/// 数据包协议版本，1~3
		/// </summary>
		/// <remarks>1不使用压缩，2 gzip，3 brotli</remarks>
		public int? Protover = 3;
		/// <summary>
		/// 用户id
		/// </summary>
		public int? Uid = null;
		/// <summary>
		/// 2无需身份验证，3可以验证，也许能给房管用
		/// </summary>
		public int? Type = null;
		/// <summary>
		/// CDN Token
		/// </summary>
		[JsonPropertyName("key")]
		public string? CdnToken = null;

		public long Aid;
		public long? From;


		/// <summary>
		/// 确保值有效
		/// </summary>
		/// <remarks>将无效<see cref="type"/>改成2，<see cref="protover"/>改成3</remarks>
		public void EnsureValid()
		{
			Type = 2;
			if (Protover != null && Protover > 3  && Protover < 1)
				Protover = BDanmakuProtocol.SupportedProtocolVersion;

			From = From > 0 ? From : 7;
			Aid = Aid == 0 ? 0 : Aid;
		}
	}
}
