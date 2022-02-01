using DanmakuR.Protocol.Model;
using System.Buffers;
using System.Runtime.CompilerServices;
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
	}
}
