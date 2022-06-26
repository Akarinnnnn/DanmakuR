using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.Compression;
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
			using BrotliDecoder decoder = new();

			SequencePosition spos = buffer.Start;
			ReadOnlySpan<byte> sourceBuffer;

			int consumed = 0, written = 0;
			OperationStatus decompressStatus;

			if (buffer.IsSingleSegment)
			{
				if (buffer.IsEmpty)
					return;

				sourceBuffer = buffer.FirstSpan;

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

			while (buffer.TryGet(ref spos, out var mem))
			{
				sourceBuffer = mem.Span;
				while (!sourceBuffer.IsEmpty)
				{
					decompressStatus = decoder.Decompress(sourceBuffer, output.GetSpan(0), out consumed, out written);
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
