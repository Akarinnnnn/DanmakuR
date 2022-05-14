﻿using DanmakuR.Buffer;
using DanmakuR.Protocol.Model;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Protocol;
using Microsoft.Extensions.Options;
using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.ExceptionServices;
using System.IO;
using System.IO.Compression;
using System.Numerics;
using System.Text.Json;

using Microsoft.AspNetCore.Internal;
using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using DanmakuR.Resources;
using System.Text;
using Microsoft.AspNetCore.SignalR.Client;

namespace DanmakuR.Protocol
{
	internal partial class BDanmakuProtocol : IHubProtocol, IHandshakeProtocol
	{
		// int32 framelength, int16 headerlength, int16 version, int32 opcode, int32 seqid
		private static readonly byte[] ping_message =
		{
			0, 0, 0, 16,
			0, 16,
			0, 1,
			0, 0, 0, 2,
			0, 0, 0, 1
		};
		private static ReadOnlyMemory<byte> PingMessageMemory => ping_message;

		private readonly BDanmakuOptions options;
		private readonly ILogger logger;

		public string Name => typeof(BDanmakuProtocol).FullName!;
		public int Version => 3;
		public TransferFormat TransferFormat => TransferFormat.Binary;
		private MessagePackage message_package = default;
		
		/// <summary>
		/// 请使用DI
		/// </summary>
		/// <param name="opt"></param>
		public BDanmakuProtocol(IOptions<BDanmakuOptions> opt, ILoggerFactory loggerFactory)
		{
			options = opt.Value;
			logger = loggerFactory.CreateLogger<BDanmakuProtocol>();
		}

		/// <inheritdoc/>
		public ReadOnlyMemory<byte> GetMessageBytes(HubMessage message)
		{
			if (ReferenceEquals(message, PingMessage.Instance) || message is PingMessage)
				return PingMessageMemory;

			return HubProtocolExtensions.GetMessageBytes(this, message);
		}

		/// <inheritdoc/>
		public bool IsVersionSupported(int version)
		{
			return version <= Version;
		}

		/// <inheritdoc/>
		public bool TryParseMessage(ref ReadOnlySequence<byte> input, IInvocationBinder binder, [NotNullWhen(true)] out HubMessage? message)
		{
			if (!message_package.IsEmpty)
			{
				try
				{
					Log.MultipleMessage(logger);
					message_package.AdvanceTo(ParseOne(message_package.ReadOne(), binder, out message));
					return true;
				}
				catch (JsonException e)
				{
					Log.InvalidJson(logger, e);
					message = null;
					return false;
				}
			}

			if (TrySliceInput(ref input, out var payload, out var header))
			{
				if (header.Version == FrameVersion.Int32BE || header.OpCode == OpCode.Pong)
				{
					int value;
					var span = payload.FirstSpan;
					if (span.Length >= 4)
					{
						value = BinaryPrimitives.ReadInt32BigEndian(payload.FirstSpan);
					}
					else
					{
						var r = new SequenceReader<byte>(payload);
						if (!r.TryReadBigEndian(out value))
						{
							message = null;
							return false;
						}
					}

					try
					{
						AssertMethodParamTypes(binder, nameof(WellKnownMethods.OnPopularity), WellKnownMethods.OnPopularity.ParamTypes);
						message = new InvocationMessage(nameof(WellKnownMethods.OnPopularity), new object[] { value });
						return true;
					}
					catch (Exception ex)
					{
						message = new InvocationBindingFailureMessage(null, WellKnownMethods.OnPopularity.Name, ExceptionDispatchInfo.Capture(ex));
						return true;
					}
				}

				try
				{
					if (header.Version != FrameVersion.Json)
						UnPackage(ref header, ref payload);

					message_package.AdvanceTo(ParseOne(new Utf8JsonReader(payload), binder, out message));
				}
				catch (JsonException e)
				{
					Log.InvalidJson(logger, e);
					goto fail;
				}
				catch (InvalidDataException e)
				{
					Log.InvalidData(logger, e);
					goto fail;
				}
			}
			fail:
			message = null;
			return false;
		}

		/// <summary>
		/// 解析核心
		/// </summary>
		/// <param name="reader"></param>
		/// <param name="binder"></param>
		/// <param name="msg"></param>
		/// <returns></returns>
		private SequencePosition ParseOne(Utf8JsonReader reader, IInvocationBinder binder, out HubMessage msg)
		{
			//TODO: 解析json

			return reader.Position;
		}

		// TODO: 什么玩意？
		private void UnPackage(ref FrameHeader header, ref ReadOnlySequence<byte> compressedPackage)
		{
			switch (header.Version)
			{
				case FrameVersion.Deflate:
					compressedPackage.DecompressDeflate();
					break;
				case FrameVersion.Brotli:
					compressedPackage.DecompressBrotli();
					break;
				default:
					throw new InvalidDataException(string.Format(SR.Unreconized_Compression, header.Version));
			}

			if (!compressedPackage.TryReadHeader(out header) || !TrySlicePayload(ref compressedPackage, out var opcode))
				throw new InvalidDataException(SR.Invalid_MsgBag);
			else
				message_package = new(in compressedPackage, opcode);
		}

		/// <summary>
		/// 从<paramref name="input"/>切去一个数据包
		/// </summary>
		/// <param name="input">待解析的数据流</param>
		/// <param name="payload">数据包内容</param>
		/// <param name="header"></param>
		/// <returns></returns>
		private static bool TrySliceInput(ref ReadOnlySequence<byte> input, out ReadOnlySequence<byte> payload, out FrameHeader header)
		{
			if (!(input.TryReadHeader(out header) && input.Length > header.FrameLength))
			{
				header = default;
				payload = default;
				return false;
			}

			payload = input.Slice(header.HeaderLength, header.FrameLength);
			input = input.Slice(header.FrameLength);
			return true;
		}

		/// <summary>
		/// 从数据包中切出内容
		/// </summary>
		/// <param name="input">数据包，返回<see langword="true"/>时，修改为数据包内容</param>
		/// <param name="opcode"></param>
		/// <returns></returns>
		private static bool TrySlicePayload(ref ReadOnlySequence<byte> input, out OpCode opcode)
		{
			if (!(input.TryReadHeader(out var header) && input.Length > header.FrameLength))
			{
				opcode = OpCode.Invalid;
				return false;
			}

			input = input.Slice(header.HeaderLength, header.FrameLength);
			opcode = header.OpCode;
			return true;
		}

		private static void AssertMethodParamTypes(IInvocationBinder binder, string methodName, Type[] types)
		{
			var actualTypes = binder.GetParameterTypes(methodName);
			if (!types.SequenceEqual(actualTypes))
			{
				DefaultInterpolatedStringHandler sb = new(
					(actualTypes.Count - 1) * 2, actualTypes.Count, null, 
					stackalloc char[actualTypes.Count * 24 + 8]
				);
				foreach (var type in types)
				{
					sb.AppendFormatted(type.Name);
					sb.AppendLiteral(", ");
				}
				string message = string.Format(SR.Sig_Mismatch, methodName, sb.ToStringAndClear());
				throw new ArgumentException(message);
			}
		}

		/// <inheritdoc/>
		/// <exception cref="OverflowException" />
		public void WriteMessage(HubMessage message, IBufferWriter<byte> output)
		{
			if (ReferenceEquals(message, PingMessage.Instance) || message is PingMessage)
				ping_message.CopyTo(output.GetSpan(16));

			var tempBuffer = MemoryBufferWriter.Get();
			try
			{
				FrameHeader header = new();
				WriteMessageCore(message, tempBuffer, ref header);
				output.WriteHeader(ref header);
				tempBuffer.CopyTo(output);
			}
			finally
			{
				MemoryBufferWriter.Return(tempBuffer);
			}

			throw new NotImplementedException();
		}

		private void WriteRequestMessageCore(HandshakeRequestMessage m, MemoryBufferWriter temp, ref FrameHeader header)
		{

			if (m.Protocol == Name && m.Version == Version)
			{
				switch (options.HandshakeSettings)
				{
					case Handshake3 v3:
						v3.Serialize(temp);
						break;
					default:
						options.HandshakeSettings.Serialize(temp);
						break;
				}
				header.OpCode = OpCode.ConnectAndAuth;
				header.FrameLength = checked((int)(header.HeaderLength + temp.Length));
			}
			else
			{
				throw new HubException(string.Format(SR.Protocol_Mismatch,
					m.Protocol,
					m.Version.ToString(),
					Name,
					Version.ToString()
					));
			}
		}

		private void WriteMessageCore(HubMessage message, MemoryBufferWriter temp, ref FrameHeader header)
		{
			switch (message)
			{
				case HandshakeRequestMessage m:
					WriteRequestMessageCore(m, temp, ref header);
					return;
				// 这不科学
				case HandshakeResponseMessage:
				case CloseMessage:
					break;

				default:
					throw new HubException(SR.Unsupported_Message);
			}

			header.FrameLength = checked((int)(header.HeaderLength + temp.Length));
		}

		public void WriteRequestMessage(HandshakeRequestMessage requestMessage, IBufferWriter<byte> output)
		{
			MemoryBufferWriter writer = MemoryBufferWriter.Get();
			FrameHeader hreder = new();
			try
			{
				WriteRequestMessageCore(requestMessage, writer, ref hreder);
			}
			finally
			{
				MemoryBufferWriter.Return(writer);
			}
		}

		public bool TryParseResponseMessage(ref ReadOnlySequence<byte> buffer, [NotNullWhen(true)] out HandshakeResponseMessage? responseMessage)
		{
			bool result = TrySliceInput(ref buffer, out var response, out FrameHeader header);
			if (!result)
			{
				responseMessage = null;
				return false;
			}

			responseMessage = new HandshakeResponseMessage(null);
			return true;
		}
	}
}
