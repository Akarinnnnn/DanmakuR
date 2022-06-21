using DanmakuR.Protocol.Model;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace DanmakuR.Protocol
{
	/// <summary>
	/// 
	/// </summary>
	/// <remarks>直接创建实例可能导致不可预料的后果</remarks>
	public class BLiveOptions
	{
#pragma warning disable CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑声明为可以为 null。
		public JsonSerializerOptions SerializerOptions { get; set; }
		public Handshake2 HandshakeSettings { get; set; }
#pragma warning restore CS8618 // 正常来说，通过DI获得的实例不会为空。直接创建的实例除外
		public TransportTypes TransportType { get; set; }
	}
}
