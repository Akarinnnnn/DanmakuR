using DanmakuR.Protocol.Model;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace DanmakuR.Protocol
{
	public class BDanmakuOptions
	{
		public JsonSerializerOptions SerializerOptions { get; set; }
		public Handshake2 HandshakeSettings { get; set; }
		public BDanmakuOptions(IOptions<JsonSerializerOptions?> serializerOptions, IOptions<Handshake2> handshake)
		{
			SerializerOptions = serializerOptions.Value	?? new(JsonSerializerDefaults.General);
			HandshakeSettings = handshake.Value;
		}
	}
}
