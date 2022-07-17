using System.Runtime.InteropServices;

namespace DanmakuR.HandshakeProxy.Tests;

public class ContextTest
{

	private static async Task HandshakeAsync(HandshakeProxyConnection connection)
	{
		WriteAppRequest(connection.Transport.Output);
		var flushResult = await connection.Transport.Output.FlushAsync();
		Assert.False(flushResult.IsCompleted, "服务端（或代理）过早断开连接");
		var input = connection.Transport.Input;
		var msgBuff = new byte[8];
		while (true)
		{
			var result = await input.ReadAsync().ConfigureAwait(false);
			var buffer = result.Buffer;
			SequencePosition consumed = buffer.Start, examined = buffer.End;

			try
			{
				if (buffer.Length == 8)
				{
					new SequenceReader<byte>(buffer).TryCopyTo(msgBuff);
					consumed = buffer.GetPosition(8, consumed);
					examined = consumed;
					Assert.True(msgBuff.AsSpan().SequenceEqual(AppDesiredResponse));
					break;
				}

				Assert.False(result.IsCompleted, "服务器未发送响应");
			}
			finally
			{
				input.AdvanceTo(consumed, examined);
			}
		}
	}

	private static async Task RunServerLoops(PipeHolder pipes)
	{
		while (true)
		{
			var received = await pipes.Input.ReadAsync().ConfigureAwait(false);
			var buffer = received.Buffer;
			if(buffer.Length >= 8)
			{
				byte[] msgBuff = new byte[8];
				new SequenceReader<byte>(buffer).TryCopyTo(msgBuff);
				Assert.True(msgBuff.AsSpan().SequenceEqual(ServerDesiredRequest));
				var pos = buffer.GetPosition(8);
				pipes.Input.AdvanceTo(pos, pos);
				break;
			}
		}

		SvWriteResponse(pipes.Output);
		await pipes.Output.FlushAsync();

		SvSendMessage(pipes.Output);
		var result = await pipes.Output.FlushAsync();

		Assert.False(result.IsCompleted);
	}

	[Fact]
	public async Task HandshakeAsyncTest()
	{
		PipeOptions options = new(useSynchronizationContext: false);
		Pipe svTransport = new(options);
		Pipe svApplication = new(options);

		PipeHolder sv = new()
		{
			Input = svTransport.Reader,
			Output = svApplication.Writer
		};

		TestConnectionContext clientBacking = new(new PipeHolder
		{
			Input = svApplication.Reader,
			Output = svTransport.Writer
		});

		HandshakeProxyConnection testee = new(clientBacking, new HandshakeProxyConnectionOptions
		{
			TransformRequest = TransformAppRequest,
			TransformResponse = TransformServerResponse
		});

		var svTask = RunServerLoops(sv);
		await HandshakeAsync(testee);
		while (true)
		{
			var readResult = await testee.Transport.Input.ReadAsync();
			var buffer = readResult.Buffer;
			var consumed = buffer.Start;
			var examined = buffer.End;

			if(buffer.Length >= FirstMessage.Length * 2)
			{
				var message = string.Create(FirstMessage.Length, buffer, static (span, b) =>
				{
					new SequenceReader<byte>(b).TryCopyTo(MemoryMarshal.Cast<char, byte>(span));
				});
				Assert.Equal(FirstMessage, message);
				break;
			}

			Assert.False(readResult.IsCompleted);
		}		
		await svTask;
	}

}
