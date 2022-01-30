using DanmakuR.Protocol.Model;
using System.Buffers;
using static System.Buffers.Binary.BinaryPrimitives;

namespace DanmakuR.Protocol
{
	internal static class BufferExtensions
	{
		internal static void WritePayloadHeader(this IBufferWriter<byte> buff, ref FrameHeader header)
		{
			Span<byte> span = buff.GetSpan(16);
			WriteInt32BigEndian(span, header.FrameLength);
			WriteInt16BigEndian(span, header.HeaderLength);
			WriteInt16BigEndian(span, (short)header.Version);
			WriteInt32BigEndian(span, (int)header.OpCode);
			WriteInt32BigEndian(span, header.UnknownField);
			buff.Advance(16);
			// BitOperations.
		}
	}
}
