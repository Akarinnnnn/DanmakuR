using DanmakuR.Protocol.Model;
using DanmakuR.Protocol.Resources;
using System.Buffers;
using System.IO.Compression;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using static System.Buffers.Binary.BinaryPrimitives;

namespace DanmakuR.Protocol.Buffer
{
	public static class BufferExtensions
	{

		// 排除CR LF，NUL、RS放前边
		private static readonly byte[] delimiters =
		{
			0, 30, 1, 2, 3, 4, 5, 6, 7, 8, 9,
			/*10 LF,*/ 11, 12, /*13 CR,*/ 14, 15, 16, 17, 18, 19, 20,
			21, 22, 23, 24, 25, 26, 27, 28, 29, 31
		};
		internal static ReadOnlySpan<byte> Delimiters => delimiters;

		internal static SequencePosition? FindDelimiterMultiSegment(this ReadOnlySequence<byte> data)
		{
			// Adapted from dotnet/runtime/blob/6ca8c9bc0c4a5fc1082c690b6768ab3be8761b11
			// BuffersExtensions.cs
			// Licensed under MIT license
			SequencePosition position = data.Start;
			SequencePosition result = position;
			while (data.TryGet(ref position, out var memory))
			{
				int index = memory.Span.IndexOfAny(Delimiters);
				if (index != -1)
				{
					return data.GetPosition(index, result);
				}
				else if (position.GetObject() == null)
				{
					break;
				}

				result = position;
			}

			return null;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static void ReverseEndiannessIfLE(ref FrameHeader header)
		{
			if(BitConverter.IsLittleEndian)
			{
				header.FrameLength = ReverseEndianness(header.FrameLength);
				header.HeaderLength = ReverseEndianness(header.HeaderLength);
				header._version = ReverseEndianness(header._version);
				header._opcode = ReverseEndianness(header._opcode);
				header.SequenceId = ReverseEndianness(header.SequenceId);
			}
		}

		/// <remarks>
		/// 包成功，除非出bug
		/// </remarks>
		[MethodImpl(MethodImplOptions.NoInlining)]
		private static void TryReadHeaderMultiSegment(in ReadOnlySequence<byte> input, out FrameHeader header)
		{
			SequenceReader<byte> r = new(input);
			Span<byte> retrived = stackalloc byte[16];
			r.TryCopyTo(retrived);

			header = MemoryMarshal.Read<FrameHeader>(retrived);
			ReverseEndiannessIfLE(ref header);

			if (header.HeaderLength < 16)
				throw new InvalidDataException($"{nameof(FrameHeader)}.{nameof(header.HeaderLength)}过短：" +
					$"读到的值为{header.HeaderLength}；正常情况至少16");
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="input"></param>
		/// <param name="header"></param>
		/// <returns>返回<see langword="false"/>说明数据不够，等数据凑齐再说</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		internal static bool TryReadHeader(this in ReadOnlySequence<byte> input, out FrameHeader header)
		{
			if (input.Length < 16)
			{
				header = default;
				return false;
			}

			ReadOnlySpan<byte> firstSpan = input.FirstSpan;
			if (firstSpan.Length >= 16)
			{
				header = MemoryMarshal.Read<FrameHeader>(firstSpan);
				ReverseEndiannessIfLE(ref header);
				return true;
			}
			else
			{
				TryReadHeaderMultiSegment(input, out header);
				return true;
			}
		}
		internal static void WriteToOutput(this FrameHeader header, IBufferWriter<byte> buff)
		{
			Span<byte> span = buff.GetSpan(Unsafe.SizeOf<FrameHeader>());
			ReverseEndiannessIfLE(ref header);
			MemoryMarshal.Write(span, ref header);
			buff.Advance(header.HeaderLength);
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="data">解压后的数据</param>
		/// <exception cref="InvalidDataException"><paramref name="data"/>包含无效数据</exception>
		public static void DecompressDeflate(ref this ReadOnlySequence<byte> data, out ReadOnlySequence<byte> decompressed)
		{
			ReadOnlySequenceStream src = new(ref data);
			using DeflateStream decoder = new(src, CompressionMode.Decompress);
			int estmatedBuffSize = unchecked((int)Math.Min(data.Length * 3, 16384));

			Memory<byte> currentDecompressed = new byte[estmatedBuffSize];
			int decodedLength = decoder.Read(currentDecompressed.Span);
			int isended = decoder.ReadByte();
			if (isended == -1)
			{
				decompressed = new(currentDecompressed[..decodedLength]);
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
					decompressed = new ReadOnlySequence<byte>(first, 0, last, last.Memory.Length);
				else
					decompressed = new ReadOnlySequence<byte>(first.Memory);
			}
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="data">解压后的数据</param>
		/// <exception cref="InvalidDataException"><paramref name="data"/>包含无效数据</exception>
		public static void DecompressBrotli(ref this ReadOnlySequence<byte> data, out ReadOnlySequence<byte> decompressed)
		{
			RentBuffer temp = new();
			using BrotliDecoder decoder = new();
			int estmatedBuffSize = unchecked((int)Math.Min(data.Length * 3, 16384));

			// TODO 用MemoryBufferWriter重写
			if (data.IsSingleSegment)
			{
				int totalConsumed = 0;
				int totalWritten = 0;
				// 看了一圈源码，IsSingleSegment时FirstSpan走的是慢路径
				// 链接 https://source.dot.net/#System.Memory/System/Buffers/ReadOnlySequence.Helpers.cs
				// FirstSpan直接跳到GetFirstSpan，再转GetFirstSpanSlow
				ReadOnlySpan<byte> dataSpan = data.FirstSpan;
			rerun:
				temp.Reset(estmatedBuffSize, true);

				var status = decoder.Decompress(dataSpan[totalConsumed..], temp.Span[totalWritten..],
					out int consumed, out int written);
				switch (status)
				{
					case OperationStatus.Done:
						decompressed = new(temp.Buff, 0, written);
						return;
					case OperationStatus.DestinationTooSmall:
						totalConsumed += consumed;
						totalWritten += written;
						estmatedBuffSize += (data.FirstSpan.Length - totalConsumed) * 4;
						goto rerun;
					case OperationStatus.NeedMoreData:
						data = data.Slice(totalConsumed);

						throw new InvalidOperationException("需要更多数据");
					case OperationStatus.InvalidData:
						throw new InvalidDataException("brotli数据损坏");
					default:
#pragma warning disable CA2208 // nameof(status)
						throw new ArgumentOutOfRangeException(nameof(status), SysSR.ArgumentOutOfRange_Enum);
#pragma warning restore CA2208 // 枚举值不在范围内
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
					// IMPROVE [PERF]: 感觉没啥必要，brDecoder自带32K缓冲区
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
					decompressed = new ReadOnlySequence<byte>(first, 0,
						(last ?? first).SetNext(currentStore[..currentStored], runningIndex),
						currentStored);
				else
					decompressed = new ReadOnlySequence<byte>(firstbuffer.AsMemory(0, unchecked((int)totalWritten)));
				return;
			}
		}

	}
}
