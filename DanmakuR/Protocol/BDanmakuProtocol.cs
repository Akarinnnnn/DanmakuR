using DanmakuR.Buffer;
using DanmakuR.Protocol.Model;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Protocol;
using Microsoft.Extensions.Options;
using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.IO.Compression;
using System.Numerics;
using System.Text.Json;

namespace DanmakuR.Protocol
{
	internal class BDanmakuProtocol
	{
		// int32 framelength, int16 headerlength, int16 version, int32 opcode, int32 seqid
		private static readonly byte[] ping_message =
		{
			0, 0, 0, 16,
			0, 16,
			0, 1,
			0, 0, 0, 2,
			0, 0, 0, 0
		};
		private readonly BDanmakuOptions options;
		public string Name => typeof(BDanmakuProtocol).FullName!;
		public int Version => 0;
		public TransferFormat TransferFormat => TransferFormat.Binary;
		internal const byte rs = 0x1e;
		public BDanmakuProtocol(IOptions<BDanmakuOptions> opt)
		{
			options = opt.Value;
		}

		private void TryBindFromJson(ref ReadOnlySequence<byte> json, IInvocationBinder binder, out HubMessage? msg)
		{
			//TODO: 解析json
			msg = null;
		}

		/// <inheritdoc/>
		public ReadOnlyMemory<byte> GetMessageBytes(HubMessage message)
		{
			if (message is PingMessage) return ping_message;

			return HubProtocolExtensions.GetMessageBytes(this, message);
		}

		/// <inheritdoc/>
		public bool IsVersionSupported(int version)
		{
			return version == Version;
		}

		/// <inheritdoc/>
		public bool TryParseMessage(ref ReadOnlySequence<byte> input, IInvocationBinder binder, [NotNullWhen(true)] out HubMessage? message)
		{
			SequenceReader<byte> r = new(input);
			using RentBuffer decompressed = new();
			message = null;
			if (r.TryReadPayloadHeader(out FrameHeader header))
			{
				if (header.Version == FrameVersion.Int32BE || header.OpCode == OpCode.Pong)
				{
					r.TryReadBigEndian(out int value);
					if (CheckMethodParamTypes(binder, WellKnownMethods.OnPopularity.Name, WellKnownMethods.OnPopularity.ParamTypes))
					{
						message = new InvocationMessage(WellKnownMethods.OnPopularity.Name, new object[] { value });
						return true;
					}
				}

				ReadOnlySequence<byte> payload = input.Slice(header.HeaderLength);
				switch (header.Version)
				{
					case FrameVersion.Deflate:
						payload.DecompressDeflate();
						break;
					case FrameVersion.Brotli:
						payload.DecompressBrotli();
						break;
					case FrameVersion.Json:
						break;
					default:
						return false;
				}

				TryBindFromJson(ref payload, binder, out message);

			}
			else
			{
				return false;
			}

			return message != null;
		}

		private static bool CheckMethodParamTypes(IInvocationBinder binder, string methodName, Type[] types)
		{
			var actualTypes = binder.GetParameterTypes(methodName);
			return types.SequenceEqual(actualTypes);
		}

		/// <inheritdoc/>
		public void WriteMessage(HubMessage message, IBufferWriter<byte> output)
		{
			switch (message)
			{
				case HandshakeRequestMessage m:
					if (m.Protocol == Name && m.Version == Version)
					{
						switch (options.HandshakeSettings)
						{
							case Handshake3 v3:
								v3.Serialize(output);
								break;
							default:
								options.HandshakeSettings.Serialize(output);
								break;
						}						
					}
					else
					{
						throw new HubException("protocol_mismatch");
					}
					break;
				case HandshakeResponseMessage:
				case CloseMessage:
					break;

				default:
					throw new HubException("unreconized_message");
			}



			throw new NotImplementedException();
		}
	}
}
