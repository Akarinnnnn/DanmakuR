using System.Buffers;
using System.IO.Pipelines;

namespace DanmakuR.HandshakeProxy.Tests
{
	public class ProxyPipeTests
	{
		public class PipeHolder : IDuplexPipe
		{
			public PipeReader Input { get; set; } = null!;
			public PipeWriter Output { get; set; } = null!;
		}

		private static readonly byte[] AppRequest = { 1, 2, 3, 4, 4, 3, 2, 1 };
		private static readonly byte[] TransformedRequest = { 1, 2, 3, 4, 1, 2, 3, 4 };
		private static readonly byte[] AppDesiredResponse = { 1, 1, 2, 2, 3, 3, 4, 4 };

		private static (bool, SequencePosition) TransformAppRequest(ReadOnlySequence<byte> request, IBufferWriter<byte> transport)
		{
			if (request.Length < 8)
				return (false, request.Start);

			Assert.True(AppRequest.AsSpan().SequenceEqual(request.FirstSpan[..8]));
			TransformedRequest.CopyTo(transport.GetMemory(8));
			transport.Advance(8);
			return (true, request.GetPosition(AppRequest.LongLength));
		}

		private static (bool, SequencePosition) TransformResponse(ReadOnlySequence<byte> response, IBufferWriter<byte> application)
		{
			AppDesiredResponse.CopyTo(application.GetMemory(8));
			application.Advance(8);
			return (true, response.End);
		}

		[Fact]
		public async Task TestSendingFromApp()
		{
			PipeHolder source = new();

			Pipe transport = new();
			source.Output = transport.Writer;

			ProxyDuplexPipe testee = new(source, static (b, _) => (true, b.Start), TransformAppRequest);

			testee.Output.Write(AppRequest);
			await testee.Output.FlushAsync();
			var result = await transport.Reader.ReadAsync();
			Assert.True(result.Buffer.FirstSpan[..8].SequenceEqual(TransformedRequest));
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

			ProxyDuplexPipe testee = new(source, TransformResponse, static (b, _) => (true, b.Start));
			var result = await testee.Input.ReadAsync();

			Assert.True(AppDesiredResponse.AsSpan().SequenceEqual(result.Buffer.FirstSpan));
		}
	}
}