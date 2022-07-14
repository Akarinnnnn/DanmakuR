using DanmakuR.Protocol.Buffer.Writers;
using Microsoft.AspNetCore.SignalR.Protocol;
using System.Buffers;
using System.Threading.Channels;

namespace DanmakuR.Protocol
{
	public sealed class ParsingAggreatedMessageState : IDisposable
	{
		private MemoryBufferWriter.WrittenSequence memory_holder;
		private bool is_disposed;
		public bool IsBrotli { get; }
		public BLiveProtocol HubProtocol { get; }

		public ReadOnlySequence<byte> Buffer { get => memory_holder.GetSequence(); }

		public ChannelWriter<HubMessage> Writer { get; }

		internal ParsingAggreatedMessageState(BLiveProtocol hubProtocol, 
			ChannelWriter<HubMessage> writer, 
			MemoryBufferWriter.WrittenSequence memoryHolder, 
			bool isBrotli)
		{
			HubProtocol = hubProtocol;
			Writer = writer;
			memory_holder = memoryHolder;
			IsBrotli = isBrotli;
		}

		private void Dispose(bool disposing)
		{
			if (!is_disposed)
			{
				if (disposing)
				{
					memory_holder.Dispose();
					memory_holder = default;
				}

				is_disposed = true;
			}
		}

		public void Dispose()
		{
			// 不要更改此代码。请将清理代码放入“Dispose(bool disposing)”方法中
			Dispose(disposing: true);
			GC.SuppressFinalize(this);
		}
	}
}
