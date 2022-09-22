using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.Compression;
using System.IO.Pipelines;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace DanmakuR.Protocol
{
	internal static class CompressionExtensions
	{
		public static void DecompressBrotli(in this ReadOnlySequence<byte> buffer, IBufferWriter<byte> output)
		{
			if (buffer.IsEmpty)
				return;

			if (buffer.IsSingleSegment)
			{
				BrSingleSegment(buffer.FirstSpan, output);
			}
			else
			{
				BrMultiSegment(buffer, output);
			}
		}

		public static void DecompressDeflate(in this ReadOnlySequence<byte> buffer, IBufferWriter<byte> output)
		{
			Stream src = PipeReader.Create(buffer).AsStream(true);
			using DeflateStream decoder = new(src, CompressionMode.Decompress, false);

			using GZipStream gzDecoder = new(src, CompressionMode.Decompress, false);
			ByStreamDecoder(output, gzDecoder);
		}

		private static void ByStreamDecoder(IBufferWriter<byte> output, Stream decoder)
		{
			int canDecompressMarker;

			while ((canDecompressMarker = decoder.ReadByte()) != -1)
			{
				var span = output.GetSpan();
				span[0] = unchecked((byte)canDecompressMarker);
				int written = decoder.Read(span[1..]);
				output.Advance(written);
			}
		}

		private static void BrSingleSegment(ReadOnlySpan<byte> sourceBuffer, IBufferWriter<byte> output)
		{
			using BrotliDecoder decoder = new();
			int consumed = 0, written = 0;
			OperationStatus decompressStatus;
			Unsafe.SkipInit(out decompressStatus);

			while (!sourceBuffer.IsEmpty || decompressStatus == OperationStatus.DestinationTooSmall)
			{
				decompressStatus = decoder.Decompress(sourceBuffer, output.GetSpan(), out consumed, out written);
				output.Advance(written);

				if (decompressStatus == OperationStatus.Done)
					return;
				else if (decompressStatus == OperationStatus.InvalidData)
					throw new InvalidDataException();

				sourceBuffer = sourceBuffer[consumed..];
			}
		}

		private static void BrMultiSegment(ReadOnlySequence<byte> buffer, IBufferWriter<byte> output)
		{
			SequencePosition spos = buffer.Start;
			using BrotliDecoder decoder = new();
			OperationStatus decompressStatus;
			Unsafe.SkipInit(out decompressStatus);

			while (buffer.TryGet(ref spos, out var mem))
			{
				ReadOnlySpan<byte> sourceBuffer = mem.Span;
				while (!sourceBuffer.IsEmpty || decompressStatus == OperationStatus.DestinationTooSmall)
				{
					decompressStatus = decoder.Decompress(sourceBuffer, output.GetSpan(0), out int consumed, out int written);
					output.Advance(written);

					switch (decompressStatus)
					{
						case OperationStatus.Done:
							return;
						case OperationStatus.DestinationTooSmall:
							continue;
						case OperationStatus.NeedMoreData:
							break;
						case OperationStatus.InvalidData:
							throw new InvalidDataException();
						default:
							EnumOutOfRange(decompressStatus);
							return;
					}

					sourceBuffer = sourceBuffer[consumed..];
				}
			}
		}

		private static void EnumOutOfRange(OperationStatus decompressStatus)
		{
			Debug.Fail($"{nameof(OperationStatus)} = {decompressStatus}");
		}
	}
}
