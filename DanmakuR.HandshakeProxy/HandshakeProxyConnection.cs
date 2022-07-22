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
			ArgumentNullException.ThrowIfNull(options.TransformRequest);
			ArgumentNullException.ThrowIfNull(options.TransformResponse);
			originalTransport = backing.Transport;

			var pair = DuplexPipe.CreateConnectionPair(pipeOptions, pipeOptions);
			RewriteApplication = pair.Application;
			Transport = pair.Transport;
			processAppRequest = options.TransformRequest;
			processServerResponse = options.TransformResponse;
		}

		internal IDuplexPipe RewriteApplication { get; }

		public override IDuplexPipe Transport { get; set; }

		public override string ConnectionId { get => backing.ConnectionId; set => backing.ConnectionId = value; }

		public override IFeatureCollection Features => backing.Features;

		public override IDictionary<object, object?> Items { get => backing.Items; set => backing.Items = value; }

		private async Task DoReceive()
		{			
			try
			{
				using SemaphoreSlim readlock = new(1);
				bool handled = false;
				do
				{
					var result = await originalTransport.Input.ReadAsync();

					(handled, var pos) = processAppRequest(result.Buffer, RewriteApplication.Output);
					originalTransport.Input.AdvanceTo(pos);

					if (result.IsCompleted)
					{
						await RewriteApplication.Output.CompleteAsync();
						break;
					}
				} while (!handled);

				while (true)
				{
					await originalTransport.Input.CopyToAsync(RewriteApplication.Output);
				}
			}
			catch (Exception ex)
			{
				await RewriteApplication.Output.CompleteAsync(ex);
				await originalTransport.Input.CompleteAsync(ex);
			}
		}

		private async Task DoSend()
		{
			try
			{
				bool handled = false;

				do
				{
					var result = await RewriteApplication.Input.ReadAsync();

					(handled, var pos) = processServerResponse(result.Buffer, originalTransport.Output);
					RewriteApplication.Input.AdvanceTo(pos);

					if (result.IsCompleted)
					{
						await originalTransport.Output.CompleteAsync();
						break;
					}
				} while (!handled);

				while (true)
				{
					await RewriteApplication.Input.CopyToAsync(originalTransport.Output);
				}
			}
			catch (Exception ex)
			{
				await originalTransport.Output.CompleteAsync(ex);
				await RewriteApplication.Input.CompleteAsync(ex);
			}
		}

		public void Start()
		{
			// Todo: 有空再考虑把originalTransport接到Transport吧
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
			catch
			{

			}
			finally
			{
				await backing.DisposeAsync();
			}
		}
	}
}