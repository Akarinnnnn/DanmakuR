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
			MemoryMarshal.Write(span, in header);
			buff.Advance(Unsafe.SizeOf<FrameHeader>());
		}
	}
}
