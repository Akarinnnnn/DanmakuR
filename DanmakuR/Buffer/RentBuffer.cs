using System.Buffers;
using System.Runtime.CompilerServices;

namespace DanmakuR.Buffer
{
	public struct RentBuffer : IDisposable
	{
		private byte[]? buff = null;
		public byte[] Buff
		{
			[MethodImpl(MethodImplOptions.AggressiveOptimization)]
			get => buff ?? throw new ObjectDisposedException(nameof(RentBuffer));
		}

		public Span<byte> Span => Buff.AsSpan();
		public Memory<byte> Memory => Buff.AsMemory();

		public void Reset(int size, bool moveToNew = false)
		{
			byte[]? newbuff = null;
			if (size != 0)
			{
				newbuff = ArrayPool<byte>.Shared.Rent(size);
				if(moveToNew && buff != null)
					buff.AsSpan().CopyTo(newbuff);
			}
			Dispose();
			buff = newbuff;
		}

		[MethodImpl(MethodImplOptions.AggressiveOptimization)]
		public void Dispose()
		{
			if (buff != null)
			{
				ArrayPool<byte>.Shared.Return(buff);
				buff = null;
			}
		}
	}
}
