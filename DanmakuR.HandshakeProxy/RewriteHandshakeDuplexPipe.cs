using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO.Pipelines;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DanmakuR.HandshakeProxy
{
	public delegate (bool handled, SequencePosition processedPos) TransformData(ReadOnlySequence<byte> buffer, PipeWriter proxyInputWriter);

	public sealed class RewriteHandshakeDuplexPipe : IDuplexPipe
	{
		internal readonly IDuplexPipe transport;
		internal readonly (PipeReader Input, PipeWriter Output) application;
		private readonly TransformData receiveHandler;
		private readonly TransformData sendHandler;

		public RewriteHandshakeDuplexPipe(IDuplexPipe source, TransformData receivingFromTransport, TransformData applicationSendingMessage)
		{
			this.transport = source;
			receiveHandler = receivingFromTransport;
			sendHandler = applicationSendingMessage;
			var input = new Pipe();
			var output = new Pipe();

			Output = output.Writer;
			Input = input.Reader;

			application = (output.Reader, input.Writer);

			RewriteTask = BeginRewrite();
		}

		private async Task BeginRewrite()
		{
			await DoSend();
			await DoReceive();
		}

		private async Task DoReceive()
		{
			try
			{
				bool handled;
				using CancellationTokenSource cts = new();
				do
				{
					var result = await transport.Input.ReadAsync(cts.Token);

					(handled, var pos) = receiveHandler(result.Buffer, application.Output);

					if (handled)
						transport.Input.AdvanceTo(pos);

					if (result.IsCompleted)
					{
						await application.Output.CompleteAsync();
						break;
					}
				} while (!handled);
				await application.Output.FlushAsync(cts.Token); 
			}
			catch (Exception ex)
			{
				await application.Output.CompleteAsync(ex);
			}
		}

		private async Task DoSend()
		{
			try
			{
				bool handled = false;

				do
				{
					var result = await application.Input.ReadAsync();

					(handled, var pos) = sendHandler(result.Buffer, transport.Output);

					if (handled)
						application.Input.AdvanceTo(pos);

					if (result.IsCompleted)
					{
						await transport.Output.CompleteAsync();
						break;
					}
				} while (!handled);
				await transport.Output.FlushAsync();
			}
			catch (Exception ex)
			{
				await transport.Output.CompleteAsync(ex);
			}
		}

		public Task RewriteTask { get; }

		public PipeReader Input { get; }
		public PipeWriter Output { get; }

		public async ValueTask FlushAllAsync()
		{
			try
			{
				await application.Output.FlushAsync();
			}
			catch (InvalidOperationException)
			{
				application.Output.CancelPendingFlush();
				await application.Output.FlushAsync();
			}

			try
			{
				await application.Input.CopyToAsync(transport.Output);
			}
			catch (InvalidOperationException)
			{
				application.Input.CancelPendingRead();
				await application.Input.CopyToAsync(transport.Output);
			}
			
		}
	}
}
