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
		public string Platform = ".net";
		/// <summary>
		/// 数据包协议版本，1~3
		/// </summary>
		/// <remarks>1不使用压缩，2 gzip，3 brotli</remarks>
		internal int protover = 3;

		[JsonIgnore(Condition = JsonIgnoreCondition.Always)]
		public FrameVersion AcceptedPacketType
		{
			get => (FrameVersion)protover;
			set => protover = (int)value;
		}
		/// <summary>
		/// 用户id
		/// </summary>
		public int? Uid = null;
		/// <summary>
		/// 2，对应<see cref="Platform"/>和<see cref="CdnToken"/>
		/// </summary>
		public readonly int Type = 2;
		/// <summary>
		/// CDN Token
		/// </summary>
		[JsonPropertyName("key")]
		public string? CdnToken = null;

		public long? Aid;
		public long? From = null;


		/// <summary>
		/// 确保值有效
		/// </summary>
		/// <remarks>将无效<see cref="type"/>改成2，<see cref="protover"/>改成3</remarks>
		public void EnsureValid()
		{
			if (Roomid == default)
				throw new ArgumentException("未设置直播间号", nameof(Roomid));

			if (protover > 3  && protover < 1)
				protover = BLiveProtocol.SupportedProtocolVersion;
	
			if(From != null)
				From = From > 0 ? From : 7;
		}
	}
}
