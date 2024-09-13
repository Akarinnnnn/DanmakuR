using DanmakuR.Protocol.Buffer;
using Microsoft.AspNetCore.Internal;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Protocol;
using System.Buffers;
using System.Threading.Channels;

namespace DanmakuR.Protocol
{
	public sealed class ParsingAggregatedMessageState : IDisposable
	{
		private TransferrableMemoryBuffer? memory_holder;
		private bool is_disposed;
		public IInvocationBinder Binder { get; }
		public BLiveProtocol HubProtocol { get; }

		public ReadOnlySequence<byte> Buffer { get => memory_holder?.Sequence ?? throw new ObjectDisposedException(GetType().FullName); }

		public ChannelWriter<HubMessage> Writer { get; }

		internal ParsingAggregatedMessageState(BLiveProtocol hubProtocol, 
			ChannelWriter<HubMessage> writer, 
			TransferrableMemoryBuffer memoryHolder, 
			IInvocationBinder binder)
		{
			HubProtocol = hubProtocol;
			Writer = writer;
			memory_holder = memoryHolder;
			Binder = binder;
		}

		private void Dispose(bool disposing)
		{
			if (!is_disposed)
			{
				if (disposing)
				{
					memory_holder?.Dispose();
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
