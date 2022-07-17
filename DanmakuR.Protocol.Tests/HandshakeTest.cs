using DanmakuR.Protocol.Model;
using System.Text;
using System.Text.Json;

namespace DanmakuR.Protocol.Tests
{
	public class HandshakeTest
	{
		[Fact]
		public void SerializeTest()
		{
			Handshake2 obj = new();
			obj.Roomid = 114514;
			obj.Platform = "";
			obj.EnsureValid();
			ArrayBufferWriter<byte> output = new(64);

			// string refl = JsonSerializer.Serialize(obj);
			string ctx = JsonSerializer.Serialize(obj, SerializationExtensions.RecommdedOptions);

			Assert.NotEqual("{}", ctx);
		}
	}
}
