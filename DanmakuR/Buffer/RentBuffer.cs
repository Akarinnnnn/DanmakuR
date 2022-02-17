using System.Buffers;
using System.Runtime.CompilerServices;

namespace DanmakuR.Buffer
{
	/// <summary>
	/// 从<see cref="ArrayPool{T}"/>租用缓冲区
	/// </summary>
	public ref struct RentBuffer
	{
		private byte[]? buff = null;
		public byte[] Buff
		{
			[MethodImpl(MethodImplOptions.AggressiveOptimization | MethodImplOptions.AggressiveInlining)]
			get => buff ?? throw new ObjectDisposedException(nameof(RentBuffer));
		}

		public Span<byte> Span => Buff.AsSpan();
		public Memory<byte> Memory => Buff.AsMemory();

		public void Reset(int size, bool moveToNew = false)
		{
			if (buff != null && size == buff.Length)
				return;

			byte[]? newbuff = null;
			if (size != 0)
			{
				newbuff = ArrayPool<byte>.Shared.Rent(size);
				if(moveToNew && buff != null)
				{
					if (size >= buff.Length)
						buff.AsSpan().CopyTo(newbuff);
					else
						buff.AsSpan(0, size).CopyTo(newbuff);
				}
			}
			Dispose();
			buff = newbuff;
		}

		/// <summary>
		/// 归还缓冲区
		/// </summary>
		[MethodImpl(MethodImplOptions.AggressiveOptimization | MethodImplOptions.AggressiveInlining)]
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
