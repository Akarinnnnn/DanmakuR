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

		public void Reset(int size)
		{
			if (size != 0)
				buff = ArrayPool<byte>.Shared.Rent(size);
		}

		[MethodImpl(MethodImplOptions.AggressiveOptimization)]
		public void Dispose()
		{
			if (buff != null)
			{
				ArrayPool<byte>.Shared.Return(buff);
				buff = Array.Empty<byte>();
			}
		}
	}
}
