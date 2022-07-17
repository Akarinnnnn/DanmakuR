using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO.Pipelines;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DanmakuR.HandshakeProxy
{
	public delegate (bool handled, SequencePosition processedPos) TransformData(ReadOnlySequence<byte> buffer, PipeWriter proxyInputWriter);

	public sealed class ProxyDuplexPipe : IDuplexPipe
	{
		private readonly Pipe appInput;
		private readonly Pipe appOutput;
		internal readonly IDuplexPipe source;
		private readonly TransformData receiveHandler;
		private readonly TransformData sendHandler;

		public ProxyDuplexPipe(IDuplexPipe source, TransformData receivingFromTransport, TransformData applicationSendingMessage)
		{
			this.source = source;
			receiveHandler = receivingFromTransport;
			sendHandler = applicationSendingMessage;
			appInput = new Pipe();
			appOutput = new Pipe();

			Output = appOutput.Writer;
			Input = appInput.Reader;

			ProxyingTask = Task.Run(async () =>
			{
				await SendToTransport();
				await ReadTransport();
			});
		}

		private async Task ReadTransport()
		{
			try
			{
				bool handled;

				do
				{
					var result = await source.Input.ReadAsync();

					(handled, var pos) = receiveHandler(result.Buffer, appInput.Writer);

					if (handled)
						source.Input.AdvanceTo(pos);

					if (result.IsCompleted)
					{
						await appInput.Writer.CompleteAsync();
						break;
					}
				} while (!handled);
				await appInput.Writer.FlushAsync(); 
			}
			catch (Exception ex)
			{
				await appInput.Writer.CompleteAsync(ex);
			}
		}

		private async Task SendToTransport()
		{
			try
			{
				bool handled;

				do
				{
					var result = await appOutput.Reader.ReadAsync();

					(handled, var pos) = sendHandler(result.Buffer, source.Output);

					if (handled)
						appOutput.Reader.AdvanceTo(pos);

					if (result.IsCompleted)
					{
						await source.Output.CompleteAsync();
						break;
					}
				} while (!handled);
				await source.Output.FlushAsync();
			}
			catch (Exception ex)
			{
				await source.Output.CompleteAsync(ex);
			}
		}

		public Task ProxyingTask { get; }

		public PipeReader Input { get; }
		public PipeWriter Output { get; }

		public async ValueTask FlushAllAsync()
		{
			await appInput.Writer.FlushAsync();
			await appOutput.Reader.CopyToAsync(source.Output);
		}
	}
}
