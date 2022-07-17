using System.Buffers;
using System.Text.Json;
using System.Text.Json.Serialization;
using DanmakuR.Protocol.Model;

namespace DanmakuR.Protocol
{
	[JsonSerializable(typeof(Handshake2))]
	[JsonSourceGenerationOptions(IgnoreReadOnlyFields = false, 
		WriteIndented = false,
		PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
		IncludeFields = true,
		DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
	internal partial class HandshakeJsonContext : JsonSerializerContext
	{

	}

	internal static class SerializationExtensions
	{
		internal static JsonSerializerOptions RecommdedOptions => HandshakeJsonContext.Default.Options;
		internal static void Serialize(this Handshake2 handshake, IBufferWriter<byte> buffer)
		{
			using Utf8JsonWriter writer = new(buffer);
			JsonSerializer.Serialize(writer, handshake, RecommdedOptions);
		}
	}
}
