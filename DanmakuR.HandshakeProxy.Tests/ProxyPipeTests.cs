namespace DanmakuR.HandshakeProxy.Tests
{
	public class ProxyPipeTests
	{
		[Fact]
		public async Task TestSendingFromApp()
		{
			PipeHolder source = new();

			Pipe transport = new();
			source.Output = transport.Writer;

			RewriteHandshakeDuplexPipe testee = new(source, static (b, _) => (true, b.Start), TransformAppRequest);

			testee.Output.Write(TestHelpers.AppRequest);
			await testee.Output.FlushAsync();
			var result = await transport.Reader.ReadAsync();
			Assert.True(result.Buffer.FirstSpan[..8].SequenceEqual(ServerDesiredRequest));
		}

		[Fact]
		public async Task TestRecevingFromTransport()
		{
			PipeHolder source = new();
			Mock<PipeReader> transport = new();
			byte[] readBuffer = new byte[16];
			transport.Setup(w => w.ReadAsync(default))
				.Returns(() => ValueTask.FromResult(new ReadResult(default, false, false)))
				.Verifiable();

			source.Input = transport.Object;

			RewriteHandshakeDuplexPipe testee = new(source, TransformServerResponse, static (b, _) => (true, b.Start));
			var result = await testee.Input.ReadAsync();

			Assert.True(AppDesiredResponse.AsSpan().SequenceEqual(result.Buffer.FirstSpan));
		}
	}
}