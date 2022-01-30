using System.Text.Json;

namespace DanmakuR.Protocol
{
	public class BDanmakuOptions
	{
		public JsonSerializerOptions JsonSerializerOptions { get; set; }
		public BDanmakuOptions(JsonSerializerOptions jsonOpt)
		{
			JsonSerializerOptions = jsonOpt;
		}
	}
}
