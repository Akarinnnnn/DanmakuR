using System.Buffers;

namespace DanmakuR.Buffer
{
	public class SimpleSegment : ReadOnlySequenceSegment<byte>
	{
		public SimpleSegment(Memory<byte> buff, long pos)
		{
			Memory = buff;
			RunningIndex = pos;
		}

		public SimpleSegment(byte[] buff, long pos) : this(buff.AsMemory(), pos)
		{
			
		}

		public SimpleSegment SetNext(byte[] buff, long pos)
		{
			var next = new SimpleSegment(buff.AsMemory(), pos);
			Next = next;
			return next;
		}

		public SimpleSegment SetNext(Memory<byte> buff, long pos)
		{
			var next = new SimpleSegment(buff, pos);
			Next = next;
			return next;
		}
	}
}
