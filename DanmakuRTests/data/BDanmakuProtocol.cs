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
	internal class BDanmakuProtocol : IHubProtocol
	{
		private readonly BDanmakuOptions options;
		public string Name => typeof(BDanmakuProtocol).FullName!;
		public int Version => 0;
		public TransferFormat TransferFormat => TransferFormat.Binary | TransferFormat.Text;
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
			ArrayBufferWriter<byte> buff = new(256);
			WriteMessage(message, buff);
			return buff.WrittenMemory;
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
				if (header.HeaderLength > 16)
					r.Advance(header.HeaderLength - 16);

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
						if (!payload.TryDecompressDeflate())
							return false;
						break;
					case FrameVersion.Brotli:
						if (!payload.TryDecompressBrotli())
							return false;
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


		public void WriteMessage(HubMessage message, IBufferWriter<byte> output)
		{
			switch (message)
			{
				case HandshakeRequestMessage m:
					if (m.Protocol == Name && m.Version == Version)
					{
						FrameHeader header = new();
						// TODO: 获取json
						output.WritePayloadHeader(ref header);
						// TODO: 写入json
					}
					else
						throw new HubException("protocol_mismatch");

					break;
				default:
					break;
			}



			throw new NotImplementedException();
		}
	}
}
