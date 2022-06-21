using DanmakuR.Protocol.Model;
using Microsoft.AspNetCore.SignalR;
using Moq;

namespace DanmakuR.Protocol.Tests
{
	public class ProtocolTest
	{
		private readonly BLiveProtocol protocol;
		private readonly byte[] brPayload;
		private readonly byte[] contigousPackages;

		internal const string MsgSamplesPath = "./data/MsgSamples/";
		private const int NextPackageIndex = 884;

		public ProtocolTest(BLiveProtocol protocol)
		{
			this.protocol = protocol;
			contigousPackages = File.ReadAllBytes(MsgSamplesPath + "nested.br.bin");
			brPayload = File.ReadAllBytes(MsgSamplesPath + "BrotliPayload/msgpayload.br");
		}

		[Fact]
		public void SlicePayload()
		{
			var buffer = new ReadOnlySequence<byte>(contigousPackages);
			Assert.True(BLiveProtocol.TrySliceInput(in buffer, out var parseResult, out var header));
			Assert.StrictEqual(OpCode.Message, header.OpCode);
			Assert.Equal(NextPackageIndex, buffer.GetOffset(parseResult.End));
		}

		[Fact]
		public void IncompletePackage()
		{
			var buffer = new ReadOnlySequence<byte>(contigousPackages, 0, 20);
			Assert.False(BLiveProtocol.TrySliceInput(buffer, out _, out _));
		}

		[Fact]
		public void IncompleteHeader()
		{
			var buffer = new ReadOnlySequence<byte>(contigousPackages, 0, 5);
			Assert.False(BLiveProtocol.TrySliceInput(buffer, out _, out _));
		}

		[Fact]
		public void SlicingIncomplete()
		{
			var incompleteHeader = new ReadOnlySequence<byte>(contigousPackages, 0, 5);
			var incompletePackage = new ReadOnlySequence<byte>(contigousPackages, 0, 20);

			Assert.False(BLiveProtocol.TrySlicePayload(ref incompleteHeader, out _));
			Assert.False(BLiveProtocol.TrySlicePayload(ref incompletePackage, out _));
		}

		[Fact]
		public void SliceContigiousPackage()
		{
			var buffer = new ReadOnlySequence<byte>(contigousPackages);
            ReadOnlySequence<byte> parseResult = buffer;
            Assert.True(BLiveProtocol.TrySlicePayload(ref parseResult, out _));
			Assert.Equal(16, buffer.GetOffset(parseResult.Start));
			Assert.Equal(NextPackageIndex, buffer.GetOffset(parseResult.End));
		}
	}
}