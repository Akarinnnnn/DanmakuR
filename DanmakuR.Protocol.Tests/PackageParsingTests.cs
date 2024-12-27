using DanmakuR.Protocol.Model;
using Microsoft.AspNetCore.SignalR;

using static DanmakuR.Protocol.BLiveMessageParser;

namespace DanmakuR.Protocol.Tests
{
	public class PackageParsingTests
	{
		private readonly byte[] contigousPackages;

		internal const string MsgSamplesPath = "./data/MsgSamples/";
		private const int NextPackageIndex = 884;

		public PackageParsingTests()
		{
			//this.protocol = protocol;
			contigousPackages = File.ReadAllBytes(MsgSamplesPath + "nested.br.bin");
		}

		[Fact]
		public void SlicePayload()
		{
			var buffer = new ReadOnlySequence<byte>(contigousPackages);
			Assert.True(TrySliceInput(in buffer, out var parseResult, out var header));
			Assert.StrictEqual(OpCode.Message, header.OpCode);
			Assert.Equal(NextPackageIndex, buffer.GetOffset(parseResult.End));
		}

		[Fact]
		public void IncompletePackage()
		{
			var buffer = new ReadOnlySequence<byte>(contigousPackages, 0, 20);
			Assert.False(TrySliceInput(buffer, out _, out _));
		}

		[Fact]
		public void IncompleteHeader()
		{
			var buffer = new ReadOnlySequence<byte>(contigousPackages, 0, 5);
			Assert.False(TrySliceInput(buffer, out _, out _));
		}

		[Fact]
		public void SlicingIncomplete()
		{
			var incompleteHeader = new ReadOnlySequence<byte>(contigousPackages, 0, 5);
			var incompletePackage = new ReadOnlySequence<byte>(contigousPackages, 0, 20);

			Assert.False(TrySlicePayload(ref incompleteHeader, out _));
			Assert.False(TrySlicePayload(ref incompletePackage, out _));
		}

		[Fact]
		public void SliceContigiousPackage()
		{
			var buffer = new ReadOnlySequence<byte>(contigousPackages);
            ReadOnlySequence<byte> parseResult = buffer;
            Assert.True(TrySlicePayload(ref parseResult, out _));
			Assert.Equal(16, buffer.GetOffset(parseResult.Start));
			Assert.Equal(NextPackageIndex, buffer.GetOffset(parseResult.End));
		}
	}
}