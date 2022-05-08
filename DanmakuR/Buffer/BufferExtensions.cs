using DanmakuR.Protocol.Model;
using DanmakuR.Resources;
using System.Buffers;
using System.IO.Compression;
using System.Runtime.CompilerServices;
using static System.Buffers.Binary.BinaryPrimitives;

namespace DanmakuR.Buffer
{
	public static class BufferExtensions
	{
		internal static void WriteHeader(this IBufferWriter<byte> buff, ref FrameHeader header)
		{
			Span<byte> span = buff.GetSpan(16);
			WriteInt32BigEndian(span, header.FrameLength);
			WriteInt16BigEndian(span, header.HeaderLength);
			WriteInt16BigEndian(span, (short)header.Version);
			WriteInt32BigEndian(span, (int)header.OpCode);
			WriteInt32BigEndian(span, header.SequenceId);
			buff.Advance(header.HeaderLength);
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

			if (header.HeaderLength < 16)
				return false;

			return result;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		internal static bool TryReadHeader(this in ReadOnlySequence<byte> input, out FrameHeader header)
		{
			if (input.Length < 16)
			{
				header = default;
				return false;
			}

			if (input.FirstSpan.Length >= 16)
			{
				ReadOnlySpan<byte> head = input.FirstSpan[..16];
				header = new(ReadInt32BigEndian(head),
					ReadInt16BigEndian(head[4..]),
					ReadInt16BigEndian(head[6..]),
					ReadInt32BigEndian(head[8..]),
					ReadInt32BigEndian(head[12..]));

				return true;
			}
			else
			{
				SequenceReader<byte> r = new(input);
				return TryReadPayloadHeader(ref r, out header);
			}
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="data">解压后的数据</param>
		/// <exception cref="InvalidDataException"><paramref name="data"/>包含无效数据</exception>
		public static void DecompressDeflate(ref this ReadOnlySequence<byte> data)
		{
			ReadOnlySequenceStream src = new (ref data);
			using DeflateStream decoder = new(src, CompressionMode.Decompress);
			int estmatedBuffSize = unchecked((int)Math.Min(data.Length * 3, 16384));

			Memory<byte> currentDecompressed = new byte[estmatedBuffSize];
			int decodedLength = decoder.Read(currentDecompressed.Span);
			int isended = decoder.ReadByte();
			if (isended == -1)
			{
				data = new(currentDecompressed[..decodedLength]);
			}
			else
			{
				long totalWritten = decodedLength;
				SimpleSegment first = new(currentDecompressed[..decodedLength], 0);
				SimpleSegment? last = null;
				while (isended != -1)
				{
					estmatedBuffSize = unchecked((int)Math.Min((data.Length - src.Position) * 3, 16384));
					if (estmatedBuffSize == 0) estmatedBuffSize = 4096;
					currentDecompressed = new byte[estmatedBuffSize];
					currentDecompressed.Span[0] = (byte)isended;
					decodedLength = decoder.Read(currentDecompressed.Span[1..]);
					last = last?.SetNext(currentDecompressed[..(decodedLength + 1)], totalWritten) ??
						first.SetNext(currentDecompressed[..(decodedLength + 1)], totalWritten);
					totalWritten += decodedLength + 1;
					isended = decoder.ReadByte();
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
						throw new ArgumentOutOfRangeException(nameof(status), SysSR.ArgumentOutOfRange_Enum);
				}
			}
			else
			{
				SimpleSegment? first = null;
				SimpleSegment? last = null;

				byte[] firstbuffer = new byte[estmatedBuffSize];
				long runningIndex = 0;
				long totalWritten;
				SequenceReader<byte> datar = new(data);
				using RentBuffer middle = new();

				ReadOnlySpan<byte> currentSrc = datar.CurrentSpan;
				Span<byte> target = firstbuffer;
				var status = decoder.Decompress(currentSrc, target, out int consumed, out int written);
				datar.Advance(consumed);
				int currentStored = written;
				totalWritten = written;
				Memory<byte> currentStore = firstbuffer;

				while (status != OperationStatus.Done)
				{
					if (status == OperationStatus.InvalidData)
						throw new InvalidDataException();

					// 是否分配下一个缓冲区
					if (status == OperationStatus.DestinationTooSmall)
					{
						runningIndex = totalWritten;
						if (first == null)
						{
							first = new(firstbuffer.AsMemory(0, currentStored), 0);
						}
						else
						{
							last = (last ?? first).SetNext(currentStore[..currentStored], runningIndex);
						}
						currentStore = new byte[Math.Min(estmatedBuffSize, 16384)];
						currentStored = 0;
						target = currentStore.Span;
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
						datar.Advance(currentSrc.Length - consumed);
						currentSrc = datar.UnreadSpan;
						int remaining = 256 + consumed - currentSrc.Length;
						currentSrc[..remaining].CopyTo(middle.Span[remaining..]);

						// 解压中间部分
						status = decoder.Decompress(middle.Span, target, out consumed, out written);
						estmatedBuffSize -= consumed;
						totalWritten += written;
						currentStored += written;
						currentSrc = currentSrc[(consumed - remaining)..];
						switch (status)
						{
							case OperationStatus.Done:
								break;
							case OperationStatus.InvalidData:
								throw new InvalidDataException();
							default:
								continue;
						}
					}

					currentSrc = datar.UnreadSpan;

					// 解压
					status = decoder.Decompress(currentSrc, target, out consumed, out written);
					estmatedBuffSize -= consumed;
					totalWritten += written;
					currentStored += written;
					datar.Advance(consumed);
				}

				if (first != null)
					data = new ReadOnlySequence<byte>(first, 0,
						(last ?? first).SetNext(currentStore[..currentStored], runningIndex),
						currentStored);
				else
					data = new ReadOnlySequence<byte>(firstbuffer.AsMemory(0, unchecked((int)totalWritten)));
				return;
			}
		}

	}
}
