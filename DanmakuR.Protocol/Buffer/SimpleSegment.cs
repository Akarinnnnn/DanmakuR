using System.Buffers;

namespace DanmakuR.Buffer
{
	public class SimpleSegment : ReadOnlySequenceSegment<byte>
	{
		public SimpleSegment(Memory<byte> buff, long runningIndex)
		{
			Memory = buff;
			RunningIndex = runningIndex;
		}

		public SimpleSegment(byte[] buff, long runningIndex) : this(buff.AsMemory(), runningIndex)
		{
			
		}

		public SimpleSegment SetNext(byte[] buff, long runinngIndex)
		{
			var next = new SimpleSegment(buff.AsMemory(), runinngIndex);
			Next = next;
			return next;
		}

		public SimpleSegment SetNext(Memory<byte> buff, long runningIndex)
		{
			var next = new SimpleSegment(buff, runningIndex);
			Next = next;
			return next;
		}
	}
}
