using DanmakuR.Protocol.Model;
using System.Collections.Immutable;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace DanmakuR.Protocol
{
	/// <summary>
	/// 配置<see cref="BLiveProtocol"/>
	/// </summary>
	public class BLiveOptions
	{
		public JsonSerializerOptions SerializerOptions { get; set; } = new JsonSerializerOptions(JsonSerializerDefaults.General)
		{
			ReadCommentHandling = JsonCommentHandling.Skip,
			Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
		};
		public Handshake2 Handshake { get; set; } = null!;
		/// <summary>
		/// 收到（<see cref="string"/>）指定的cmd时，调用的解析函数。返回值将送到事先注册的回调函数
		/// </summary>
		public Dictionary<string, CommandBinder> CommandBinders { get; set; } = new();
		public TransportTypes TransportType { get; set; } = TransportTypes.InsecureWebsocket;
		public JsonReaderOptions ReaderOptions { get; set; } = new();
		/// <summary>
		/// 房号可能是短号
		/// </summary>
		public bool MightBeShortId { get; set; } = false;
	}
}