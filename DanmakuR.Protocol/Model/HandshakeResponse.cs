namespace DanmakuR.Protocol.Model;

using Microsoft.AspNetCore.Internal;
using Microsoft.AspNetCore.SignalR;
using System.Buffers;
using System.Runtime.CompilerServices;
using System.Text.Json;

internal class HandshakeResponse
{
	private static readonly JsonEncodedText CodePropertyName = JsonEncodedText.Encode("code");
	private const string TemplateSuccessfulJson = "{\"code\":0}";
	private static readonly JsonEncodedText TemplateSuccessful = JsonEncodedText.Encode(TemplateSuccessfulJson);
	/// <summary>
	/// 
	/// </summary>
	/// <param name="reader"></param>
	/// <returns>code，0成功其他失败</returns>
	public static int ParseResponse(Utf8JsonReader reader)
	{
		int? code = null;

		reader.CheckRead();
		reader.EnsureObjectStart();

		while (reader.Read())
		{
			switch (reader.TokenType)
			{
				case JsonTokenType.StartArray:
				case JsonTokenType.StartObject:
					reader.Skip();
					break;
				case JsonTokenType.PropertyName:
					{
						if (reader.ValueTextEquals(CodePropertyName.EncodedUtf8Bytes))
						{
							reader.Read();
							code = reader.GetInt32();

							// 剩下的全噶了
							while (reader.Read())
								reader.Skip();

							goto got;
						}
						else
						{
							reader.Skip();
							continue;
						}
					}
				default:
					continue;
			}
		}

		if (code == null)
			throw new InvalidDataException("这连的是b站吗？");

		got:
		return code.Value;
	}
	/// <summary>
	/// 确认服务器响应的是不是连接成功的样板响应 {"code":0}
	/// </summary>
	/// <param name="response"></param>
	/// <remarks>不是的话，用<see cref="ParseResponse(Utf8JsonReader)"/>进一步确认</remarks>
	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public static bool IsTemplateSuccessful(in ReadOnlySequence<byte> response)
	{
		if (response.IsSingleSegment && response.Length == TemplateSuccessfulJson.Length)
		{
			return response.FirstSpan.SequenceEqual(TemplateSuccessful.EncodedUtf8Bytes);
		}
		else
		{
			if (response.Length < TemplateSuccessfulJson.Length)
				return false;

			return IsTemplateSlow(response);
		}
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	private static bool IsTemplateSlow(in ReadOnlySequence<byte> response)
	{
		Span<byte> comparingBuffer = stackalloc byte[TemplateSuccessfulJson.Length];
		new SequenceReader<byte>(response).TryCopyTo(comparingBuffer);
		return comparingBuffer.SequenceEqual(TemplateSuccessful.EncodedUtf8Bytes);
	}
}
