using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Options;
using System.Buffers;
using System.Diagnostics;
using System.IO.Pipelines;

namespace DanmakuR.HandshakeProxy
{
	public delegate (bool handled, SequencePosition position) TransformData(ReadOnlySequence<byte> input, IBufferWriter<byte> writer);

	public sealed class RewriteHandshakeConnection : ConnectionContext
	{
		private static readonly PipeOptions pipeOptions = new(
			readerScheduler: PipeScheduler.Inline,
			writerScheduler: PipeScheduler.Inline,
			useSynchronizationContext: false
		);

		private readonly ConnectionContext backing;
		private readonly IDuplexPipe originalTransport;
		private readonly TransformData processAppRequest;
		private readonly TransformData processServerResponse;
		private Task? receiveTask;
		private Task? sendTask;

		public RewriteHandshakeConnection(ConnectionContext backing, HandshakeProxyConnectionOptions options)
		{
			this.backing = backing;
			ArgumentNullException.ThrowIfNull(options.RewriteAppRequest);
			ArgumentNullException.ThrowIfNull(options.RewriteServerResponse);
			originalTransport = backing.Transport;

			var pair = DuplexPipe.CreateConnectionPair(pipeOptions, pipeOptions);
			RewriteApplication = pair.Application;
			Transport = pair.Transport;
			processAppRequest = options.RewriteAppRequest;
			processServerResponse = options.RewriteServerResponse;
		}

		internal IDuplexPipe RewriteApplication { get; }

		public override IDuplexPipe Transport { get; set; }

		public override string ConnectionId { get => backing.ConnectionId; set => backing.ConnectionId = value; }

		public override IFeatureCollection Features => backing.Features;

		public override IDictionary<object, object?> Items { get => backing.Items; set => backing.Items = value; }

		private async Task DoReceive()
		{
			
			bool handled = false;
			do
			{
				ReadResult result = await originalTransport.Input.ReadAsync();
				var buffer = result.Buffer;
				SequencePosition consumed = buffer.Start;
				try
				{
					(handled, consumed) = processServerResponse(buffer, RewriteApplication.Output);
					originalTransport.Input.AdvanceTo(consumed, buffer.End);

					if (result.IsCompleted)
					{
						await RewriteApplication.Output.CompleteAsync();
						break;
					}

					if (handled)
					{
						Transport = originalTransport;
						await RewriteApplication.Output.FlushAsync();
					}
				}
				catch (Exception ex)
				{
					originalTransport.Input.AdvanceTo(consumed, buffer.End);
					RewriteApplication.Output.Complete(ex);
					throw;
				}
			} while (!handled);
		}

		private async Task DoSend()
		{
			bool handled = false;
			do
			{
				var result = await RewriteApplication.Input.ReadAsync();
				try
				{

					(handled, var pos) = processAppRequest(result.Buffer, originalTransport.Output);
					RewriteApplication.Input.AdvanceTo(pos);

					if (result.IsCompleted)
					{
						await originalTransport.Output.CompleteAsync();
						break;
					}

					if (handled)
					{
						await originalTransport.Output.FlushAsync();
					}
				}
				catch (Exception ex)
				{
					RewriteApplication.Input.AdvanceTo(result.Buffer.Start, result.Buffer.End);
					originalTransport.Output.Complete(ex);
					throw;
				}
			} while (!handled);
		}

		public void Start()
		{
			sendTask = DoSend();
			receiveTask = DoReceive();
		}

		public override async ValueTask DisposeAsync()
		{
			originalTransport.Input.Complete();
			originalTransport.Output.Complete();

			try
			{
				if (receiveTask != null)
				{
					await receiveTask; 
				}

				Debug.Assert(sendTask != null);
				await sendTask; 
			}
			finally
			{
				await backing.DisposeAsync();
			}
		}
	}
}