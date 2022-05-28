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
		private static readonly JsonSerializerOptions options;
		static SerializationExtensions()
		{
			options = new(JsonSerializerDefaults.General)
			{
				Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
			};
			options.AddContext<HandshakeJsonContext>();
		}
		internal static JsonSerializerOptions RecommedOptions => options;
		internal static void Serialize(this Handshake2 handshake, IBufferWriter<byte> buffer)
		{
			using Utf8JsonWriter writer = new(buffer);
			JsonSerializer.Serialize(writer, handshake, options);
		}
	}
}
