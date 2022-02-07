using System;
using System.Buffers;

namespace DanmakuR.Buffer
{
	public class ReadOnlySequenceStream : Stream
	{
		private readonly ReadOnlySequence<byte> seq;
		public ReadOnlySequenceStream(ref ReadOnlySequence<byte> buffer)
		{
			seq = buffer;
		}

		public override bool CanRead => !seq.IsEmpty && Position != seq.Length;
		public override bool CanSeek => true;
		public override bool CanWrite => false;
		public override long Length => seq.Length;
		public override long Position { get; set; }

		public override void Flush()
		{
			/* no-op */
		}

		public override int Read(byte[] buffer, int offset, int count)
		{
			ValidateBufferArguments(buffer, offset, count);
			return Read(new Span<byte>(buffer, offset, count));
		}

		public override int Read(Span<byte> buffer)
		{
			if (Position == seq.Length)
				return 0;

			SequenceReader<byte> r = new(seq);
			int read;
			r.Advance(Position);
			if(r.TryCopyTo(buffer))
				read = buffer.Length;
			else
				read = checked((int)(r.Consumed - Position));
			Position = r.Consumed;
			return read;
		}

		public override long Seek(long offset, SeekOrigin origin)
		{
			switch (origin)
			{
				case SeekOrigin.Begin:
					Position = offset;
					break;
				case SeekOrigin.Current:
					Position += offset;
					break;
				case SeekOrigin.End:
					Position = Length - offset;
					break;
				default:
					throw new ArgumentException(null, nameof(origin));
			}
			return Position;
		}

		public override void SetLength(long value)
		{
			throw new NotSupportedException();
		}

		public override void Write(byte[] buffer, int offset, int count)
		{
			throw new NotSupportedException();
		}

#pragma warning disable CA1816 // Dispose 方法应调用 SuppressFinalize
		public override ValueTask DisposeAsync()
		{
			return new ValueTask();
		}
		public override void Close()
		{
			/* no-op */
		}
#pragma warning restore CA1816 // Dispose 方法应调用 SuppressFinalize

	}
}
