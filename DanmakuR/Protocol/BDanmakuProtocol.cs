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

using Microsoft.AspNetCore.Internal;

namespace DanmakuR.Protocol
{
	internal class BDanmakuProtocol : IHubProtocol
	{
		// int32 framelength, int16 headerlength, int16 version, int32 opcode, int32 seqid
		private static readonly byte[] ping_message_header =
		{
			0, 0, 0, 16,
			0, 16,
			0, 1,
			0, 0, 0, 2,
			0, 0, 0, 1
		};

		private readonly BDanmakuOptions options;
		public string Name => typeof(BDanmakuProtocol).FullName!;
		public int Version => 0;
		public TransferFormat TransferFormat => TransferFormat.Binary;
		internal static readonly byte[] seperators = new byte[0x20];

		private ReadOnlySequence<byte> aggreated_messages = ReadOnlySequence<byte>.Empty;
		private OpCode package_opcode = OpCode.Invalid;
		static BDanmakuProtocol()
		{
			for (byte i = 0; i < seperators.Length; i++)
				seperators[i] = i;
		}

		public BDanmakuProtocol(IOptions<BDanmakuOptions> opt)
		{
			options = opt.Value;
		}

		private bool TryBindFromJson(ref ReadOnlySequence<byte> json, IInvocationBinder binder, out HubMessage? msg)
		{
			//TODO: 解析json
			msg = null;
			return false;
		}

		/// <inheritdoc/>
		public ReadOnlyMemory<byte> GetMessageBytes(HubMessage message)
		{
			if (message is PingMessage) return ping_message_header;

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
			if (!aggreated_messages.IsEmpty)
				return TryBindFromJson(ref aggreated_messages, binder, out message);

			SequenceReader<byte> r = new(aggreated_messages.IsEmpty ? aggreated_messages : input);
			message = null;
			if (r.TryReadPayloadHeader(out FrameHeader header))
			{
				if (input.Length < header.FrameLength)
					return false;

				ReadOnlySequence<byte> payload = input.Slice(header.HeaderLength, header.FrameLength);
				input = input.Slice(header.HeaderLength + header.FrameLength + 1);

				if (header.Version == FrameVersion.Int32BE || header.OpCode == OpCode.Pong)
				{
					r.Advance(header.HeaderLength);
					r.TryReadBigEndian(out int value);
					if (CheckMethodParamTypes(binder, WellKnownMethods.OnPopularity.Name, WellKnownMethods.OnPopularity.ParamTypes))
					{
						message = new InvocationMessage(WellKnownMethods.OnPopularity.Name, new object[] { value });
						return true;
					}
					else
					{
						return false;
					}
				}

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

				return TryBindFromJson(ref payload, binder, out message);
			}
			else
			{
				return false;
			}
		}

		private static bool CheckMethodParamTypes(IInvocationBinder binder, string methodName, Type[] types)
		{
			var actualTypes = binder.GetParameterTypes(methodName);
			return types.SequenceEqual(actualTypes);
		}

		/// <inheritdoc/>
		/// <exception cref="OverflowException" />
		public void WriteMessage(HubMessage message, IBufferWriter<byte> output)
		{
			if (ReferenceEquals(message, PingMessage.Instance) || message is PingMessage)
				ping_message_header.CopyTo(output.GetSpan(16));

			var tempBuffer = MemoryBufferWriter.Get();
			FrameHeader header = new();
			WriteMessageCore(message, tempBuffer, ref header);
			output.WriteHeader(ref header);
			tempBuffer.CopyTo(output);

			MemoryBufferWriter.Return(tempBuffer);

			throw new NotImplementedException();
		}

		private void WriteMessageCore(HubMessage message, MemoryBufferWriter temp, ref FrameHeader header)
		{
			switch (message)
			{
				case HandshakeRequestMessage m:
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
					}
					else
					{
						throw new HubException("protocol_mismatch");
					}
					break;
				// 这不科学
				case HandshakeResponseMessage:
				case CloseMessage:
					break;

				default:
					throw new HubException("unreconized_message");
			}

			header.FrameLength = checked((int)(header.HeaderLength + temp.Length));
		}
	}
}
