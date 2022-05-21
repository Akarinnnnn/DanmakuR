﻿namespace DanmakuR.Protocol.Model;

using Microsoft.AspNetCore.Internal;
using Microsoft.AspNetCore.SignalR;
using System.Text.Json;

internal class HandshakeResponse
{
	private static readonly JsonEncodedText CodePropertyName = JsonEncodedText.Encode("code");

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
}
