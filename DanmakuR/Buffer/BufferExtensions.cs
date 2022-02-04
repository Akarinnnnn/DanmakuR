using DanmakuR.Protocol.Model;
using System.Buffers;
using System.IO.Compression;
using System.Runtime.CompilerServices;
using static System.Buffers.Binary.BinaryPrimitives;

namespace DanmakuR.Buffer
{
	public static class BufferExtensions
	{
		internal static void WritePayloadHeader(this IBufferWriter<byte> buff, ref FrameHeader header)
		{
			Span<byte> span = buff.GetSpan(16);
			WriteInt32BigEndian(span, header.FrameLength);
			WriteInt16BigEndian(span, header.HeaderLength);
			WriteInt16BigEndian(span, (short)header.Version);
			WriteInt32BigEndian(span, (int)header.OpCode);
			WriteInt32BigEndian(span, header.SequenceId);
			buff.Advance(16);
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="r"></param>
		/// <param name="header">返回<see langword="false"/>时可能含有未初始化的数据</param>
		/// <returns><see langword="true"/>成功</returns>
		internal static bool TryReadPayloadHeader(this ref SequenceReader<byte> r, out FrameHeader header)
		{
			Unsafe.SkipInit(out header);
			bool result = r.TryReadBigEndian(out header.FrameLength) &&
				r.TryReadBigEndian(out header.HeaderLength) &&
				r.TryReadBigEndian(out header._version) &&
				r.TryReadBigEndian(out header._opcode) &&
				r.TryReadBigEndian(out header.SequenceId);

			var tail = header.HeaderLength - Unsafe.SizeOf<FrameHeader>();
			if (tail < 0)
				return false;
			if (tail > 0)
				r.Advance(tail);

			return result;
		}

		public static bool TryDecompressDeflate(ref this ReadOnlySequence<byte> data)
		{
			try
			{
				using DeflateStream stream = new(new ReadOnlySequenceStream(ref data), CompressionMode.Decompress);
				int estmateSize = checked((int)data.Length) * 2;
				if (estmateSize > 4096) estmateSize = 4096;

				RentBuffer decompressed = new();
				decompressed.Reset(estmateSize);
				int decodedLength = stream.Read(decompressed.Buff.AsSpan());
				if (!stream.CanRead)
				{
					data = new(decompressed.Buff.AsMemory(16));
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
						data = new ReadOnlySequence<byte>(first, 0, last, last.Memory.Length);
					else
						data = new ReadOnlySequence<byte>(first.Memory);
				}

			}
			catch (Exception)
			{
				return false;
			}
			return true;
		}

		public static bool TryDecompressBrotli(ref this ReadOnlySequence<byte> data)
		{
			RentBuffer decompressed = new();
			using BrotliDecoder decoder = new();
			int estmateSize = checked((int)data.Length) * 3;

			if (data.IsSingleSegment)
			{
				if (estmateSize > 4096) estmateSize = 8192;
				int totalConsumed = 0;
				int totalWritten = 0;
			rerun:
				decompressed.Reset(estmateSize, true);

				var status = decoder.Decompress(data.FirstSpan[totalConsumed..], decompressed.Span[totalWritten..],
					out int consumed, out int written);
				switch (status)
				{
					case OperationStatus.Done:
						data = new(decompressed.Buff.AsMemory(0, written));
						return true;
					case OperationStatus.DestinationTooSmall:
						totalConsumed += consumed;
						totalWritten += written;
						estmateSize += (data.FirstSpan.Length - totalConsumed) * 4;
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
				SequenceReader<byte> payloadr = new(data);
				using RentBuffer middle = new();
				middle.Reset(64);

				ReadOnlySpan<byte> currentSpan = payloadr.CurrentSpan;
				Span<byte> target = firstbuffer;
				var status = decoder.Decompress(currentSpan, target, out int consumed, out int written);
				payloadr.Advance(currentSpan.Length);

				first = new(firstbuffer.AsMemory(0, written), 0);
				if (status == OperationStatus.InvalidData)
				{
					return false;
				}
				while (status != OperationStatus.Done)
				{
					int remaining = currentSpan.Length - consumed;
					estmateSize -= consumed;
					byte[] nextbuff = new byte[Math.Min(estmateSize, 8192)];
					// 接起第一段剩下的和下一段的开头，放到middle中
					currentSpan[consumed..].CopyTo(middle.Span);
					currentSpan = payloadr.CurrentSpan;
					currentSpan[..remaining].CopyTo(middle.Span[remaining..]);
					// 解压中间部分
					status = decoder.Decompress(middle.Span, nextbuff, out consumed, out written);

					switch (status)
					{
						case OperationStatus.Done:
							return true;
						case OperationStatus.InvalidData:
							return false;
					}

					currentSpan = currentSpan[(consumed - remaining)..];
					// 第二轮解压
					status = decoder.Decompress(currentSpan, nextbuff.AsSpan()[written..], out consumed, out written);
					// TODO
					switch (status)
					{
						case OperationStatus.Done:
							return true;
						case OperationStatus.InvalidData:
							return false;
					}
				}

				if (last != null)
					data = new ReadOnlySequence<byte>(first, 0, last, last.Memory.Length);
				else
					data = new ReadOnlySequence<byte>(first.Memory);
				return true;
			}
		}

	}
}
