using System.Runtime.InteropServices;

namespace DanmakuR.HandshakeProxy.Tests
{
	internal static class TestHelpers
	{
		internal static readonly byte[] AppRequest = { 1, 2, 3, 4, 4, 3, 2, 1 };
		internal static readonly byte[] ServerDesiredRequest = { 1, 2, 3, 4, 1, 2, 3, 4 };
		internal static readonly byte[] ServerSentResponse = { 1, 3, 3, 7, 4, 2, 4, 2 };
		internal static readonly byte[] AppDesiredResponse = { 1, 1, 2, 2, 3, 3, 4, 4 };
		internal const string FirstMessage = "Hello World!";


		internal static (bool, SequencePosition) TransformAppRequest(ReadOnlySequence<byte> request, IBufferWriter<byte> transport)
		{
			if (request.Length < 8)
				return (false, request.Start);

			Assert.True(AppRequest.AsSpan().SequenceEqual(request.FirstSpan[..8]));
			ServerDesiredRequest.CopyTo(transport.GetMemory(8));
			transport.Advance(8);
			return (true, request.GetPosition(AppRequest.LongLength));
		}

		internal static (bool, SequencePosition) TransformServerResponse(ReadOnlySequence<byte> response, IBufferWriter<byte> application)
		{
			Assert.True(response.FirstSpan.SequenceEqual(ServerSentResponse));
			AppDesiredResponse.CopyTo(application.GetMemory(8));
			application.Advance(8);
			return (true, response.End);
		}

		internal static void WriteAppRequest(IBufferWriter<byte> writer)
		{
			AppRequest.AsSpan().CopyTo(writer.GetSpan(AppRequest.Length));
			writer.Advance(AppRequest.Length);
		}

		internal static void SvWriteResponse(IBufferWriter<byte> writer)
		{
			ServerSentResponse.AsSpan().CopyTo(writer.GetSpan(ServerSentResponse.Length));
			writer.Advance(ServerSentResponse.Length);
		}

		internal static void SvSendMessage(IBufferWriter<byte> writer)
		{
			ReadOnlySpan<byte> span = MemoryMarshal.Cast<char, byte>(FirstMessage);
			span.CopyTo(writer.GetSpan(span.Length));
			writer.Advance(span.Length);
		}
	}
}