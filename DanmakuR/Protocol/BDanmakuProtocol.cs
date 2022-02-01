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

				if (header.Version == FrameVersion.Int32BE)
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
							if (!TryDecompressDeflate(ref payload))
								return false;
						break;
					case FrameVersion.Brotli:
							if (!TryDecompressBrotli(ref payload))
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

		private static bool TryDecompressDeflate(ref ReadOnlySequence<byte> payload)
		{
			try
			{
				using DeflateStream stream = new(new ReadOnlySequenceStream(ref payload), CompressionMode.Decompress);
				int estmateSize = checked((int)payload.Length) * 2;
				if (estmateSize > 4096) estmateSize = 4096;

				RentBuffer decompressed = new();
				decompressed.Reset(estmateSize);
				int decodedLength = stream.Read(decompressed.Buff.AsSpan());
				if (!stream.CanRead)
				{
					payload = new(decompressed.Buff.AsMemory(16));
				}
				else
				{
					long pos = decodedLength;
					SimpleSegment first = new(decompressed.Buff, 0);
					SimpleSegment? last = null;
					while (stream.CanRead)
					{
						byte[] part = new byte[8192];
						int read = stream.Read(part.AsSpan());
						pos += read;
						last = last?.SetNext(part.AsMemory(0, read), pos) ?? first.SetNext(part.AsMemory(0, read), pos);
					}
					if (last != null)
						payload = new ReadOnlySequence<byte>(first, 0, last, last.Memory.Length);
					else
						payload = new ReadOnlySequence<byte>(first.Memory);
				}

			}
			catch (Exception)
			{
				return false;
			}
			return true;
		}

		private static bool TryDecompressBrotli(ref ReadOnlySequence<byte> payload)
		{
			RentBuffer decompressed = new();
			using BrotliDecoder decoder = new();
			int estmateSize = checked((int)payload.Length) * 3;

			if (payload.IsSingleSegment)
			{
				if (estmateSize > 4096) estmateSize = 4096;
				int totalConsumed = 0;
				int totalWritten = 0;
			rerun:
				decompressed.Reset(estmateSize, true);

				var status = decoder.Decompress(payload.FirstSpan[totalConsumed..], decompressed.Span[totalWritten..],
					out int consumed, out int written);
				switch (status)
				{
					case OperationStatus.Done:
						payload = new(decompressed.Buff.AsMemory(0, written));
						return true;
					case OperationStatus.DestinationTooSmall:
						totalConsumed += consumed;
						totalWritten += written;
						estmateSize += (payload.FirstSpan.Length - totalConsumed) * 4;
						goto rerun;
					case OperationStatus.NeedMoreData:
					case OperationStatus.InvalidData:
					default:
						return false;
				}
			}
			else
			{
				SimpleSegment first;
				SimpleSegment? last = null;
				if (estmateSize > 8192) estmateSize = 16384;
				byte[] firstbuffer = new byte[estmateSize];
				// begin = new(firstbuffer, 0);
				SequenceReader<byte> payloadr = new(payload);
				using RentBuffer middle = new();
				middle.Reset(64);

				ReadOnlySpan<byte> currentSpan = payloadr.CurrentSpan;
				Span<byte> target = firstbuffer;
				var status = decoder.Decompress(currentSpan, target, out int consumed, out int written);
				first = new(firstbuffer.AsMemory(0, written), 0);
				if (status == OperationStatus.InvalidData)
				{
					return false;
				}
				while (status != OperationStatus.Done)
				{
					int remaining = currentSpan.Length - consumed;
					currentSpan[consumed..].CopyTo(middle.Span);
					// TODO
				}

				if (last != null)
					payload = new ReadOnlySequence<byte>(first, 0, last, last.Memory.Length);
				else
					payload = new ReadOnlySequence<byte>(first.Memory);
				return true;
			}
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
