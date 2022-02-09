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

		/// <summary>
		/// 
		/// </summary>
		/// <param name="data">解压后的数据</param>
		/// <exception cref="InvalidDataException"><paramref name="data"/>包含无效数据</exception>
		public static void DecompressDeflate(ref this ReadOnlySequence<byte> data)
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

		/// <summary>
		/// 
		/// </summary>
		/// <param name="data">解压后的数据</param>
		/// <exception cref="InvalidDataException"><paramref name="data"/>包含无效数据</exception>
		public static void DecompressBrotli(ref this ReadOnlySequence<byte> data)
		{
			RentBuffer decompressed = new();
			using BrotliDecoder decoder = new();
			int estmatedBuffSize = unchecked((int)Math.Min(data.Length * 3, 16384));

			if (data.IsSingleSegment)
			{
				int totalConsumed = 0;
				int totalWritten = 0;
			rerun:
				decompressed.Reset(estmatedBuffSize, true);

				var status = decoder.Decompress(data.FirstSpan[totalConsumed..], decompressed.Span[totalWritten..],
					out int consumed, out int written);
				switch (status)
				{
					case OperationStatus.Done:
						data = new(decompressed.Buff.AsMemory(0, written));
						return;
					case OperationStatus.DestinationTooSmall:
						totalConsumed += consumed;
						totalWritten += written;
						estmatedBuffSize += (data.FirstSpan.Length - totalConsumed) * 4;
						goto rerun;
					case OperationStatus.NeedMoreData:
					case OperationStatus.InvalidData:
						throw new InvalidDataException();
					default:
						throw new ArgumentOutOfRangeException("value", "enum_outofrange");
				}
			}
			else
			{
				SimpleSegment first;
				SimpleSegment? last = null;

				byte[] firstbuffer = new byte[estmatedBuffSize];
				long totalWritten = 0;
				SequenceReader<byte> payloadr = new(data);
				using RentBuffer middle = new();

				ReadOnlySpan<byte> currentSrc = payloadr.CurrentSpan;
				Span<byte> target = firstbuffer;
				var status = decoder.Decompress(currentSrc, target, out int consumed, out int written);

				first = new(firstbuffer.AsMemory(0, written), 0);

				while (status != OperationStatus.Done)
				{
					if (status == OperationStatus.InvalidData)
						throw new InvalidDataException();

					int remaining = currentSrc.Length - consumed;
					estmatedBuffSize -= consumed;
					totalWritten += written;
					// 是否分配下一个缓冲区
					if (status == OperationStatus.DestinationTooSmall)
					{
						var newbuff = new byte[Math.Min(estmatedBuffSize, 16384)];
						last = last?.SetNext(newbuff, totalWritten) ?? first.SetNext(newbuff, totalWritten);
						target = newbuff;
					}
					else
					{
						// 切一下算了
						target = target[written..];
					}
					// 第一段有点剩的
					// 接起第一段剩下的和下一段的开头，放到middle中
					if (status == OperationStatus.NeedMoreData && currentSrc.Length != consumed)
					{
						middle.Reset(256);
						currentSrc[consumed..].CopyTo(middle.Span);
						payloadr.Advance(currentSrc.Length);
						currentSrc = payloadr.CurrentSpan;
						currentSrc[..remaining].CopyTo(middle.Span[remaining..]);

						// 解压中间部分
						status = decoder.Decompress(middle.Span, target, out consumed, out written);
						estmatedBuffSize -= consumed;
						totalWritten += written;
						currentSrc = currentSrc[(consumed - remaining)..];
						switch (status)
						{
							case OperationStatus.Done:
								return;
							case OperationStatus.InvalidData:
								throw new InvalidDataException();
							default:
								continue;
						}
					}
					else
					{
						payloadr.Advance(consumed);
						currentSrc = payloadr.CurrentSpan;
					}


					// 解压
					status = decoder.Decompress(currentSrc, target, out consumed, out written);
					estmatedBuffSize -= consumed;
					totalWritten += written;
				}

				if (last != null)
					data = new ReadOnlySequence<byte>(first, 0, last, last.Memory.Length);
				else
					data = new ReadOnlySequence<byte>(first.Memory);
				return;
			}
		}

	}
}
