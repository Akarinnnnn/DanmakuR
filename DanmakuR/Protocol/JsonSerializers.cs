using System.Buffers;
using System.Text.Json;
using System.Text.Json.Serialization;
using DanmakuR.Protocol.Model;

namespace DanmakuR.Protocol
{
	[JsonSerializable(typeof(Handshake2))]
	[JsonSerializable(typeof(Handshake3))]
	[JsonSourceGenerationOptions(IgnoreReadOnlyFields = false, WriteIndented = false, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
	internal partial class HandshakeJsonContext : JsonSerializerContext
	{

	}

	internal static class SerializationExtensions
	{
		internal static void Serialize(this Handshake2 handshake, IBufferWriter<byte> buffer)
		{
			using Utf8JsonWriter writer = new(buffer);
			JsonSerializer.Serialize(writer, handshake, typeof(Handshake2), HandshakeJsonContext.Default);
		}

		internal static void Serialize(this Handshake3 handshake3, IBufferWriter<byte> buffer)
		{
			using Utf8JsonWriter writer = new(buffer);
			JsonSerializer.Serialize(writer, handshake3, typeof(Handshake3), HandshakeJsonContext.Default);
		}
	}
}
