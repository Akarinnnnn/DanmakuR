using Microsoft.VisualStudio.TestTools.UnitTesting;
using DanmakuR.Buffer;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Buffers;

namespace DanmakuR.Buffer.Tests
{
	internal static class StreamHelpers
	{
		public static void AreEqual(Stream excepted, Stream actual)
		{
			// ArrayPool<byte>.Shared.Rent
			using RentBuffer buffE = new();
			using RentBuffer buffA = new();
			buffE.Reset(8192);
			buffA.Reset(8192);
			int readE = 0, readA = 0;
			if (excepted.Length != actual.Length) throw new AssertFailedException($"excepted.Length[{excepted.Length}] != actual.Length[{actual.Length}].");

			while ((readE = excepted.Read(buffE.Span)) != 0 && (readA = actual.Read(buffA.Span)) != 0)
			{
				if(readE != readA)
					throw new AssertFailedException($"read: {readE} != {readA}.");

				if (!MemoryExtensions.SequenceEqual(buffE.Span[0..readE], buffA.Span[0..readA]))
					throw new AssertFailedException($"Difference between [{excepted.Length}..{excepted.Length + readE}].");
			}
		}
	}

	[TestClass()]
	public class BrotliTests
	{
		[TestInitialize]
		public void PrepareEnvironment()
		{
			Environment.CurrentDirectory = Path.GetDirectoryName(typeof(BrotliTests).Assembly.Location)!;
		}

		[DataRow("data/BDanmakuProtocol.cs")]
		[DataRow("data/BufferExtensions.cs")]
		[DataRow("data/Handshake2.cs")]
		[DataRow("data/SimpleSegment.cs")]
		[TestMethod()]
		public void DecompressTest(string srcName)
		{
			using FileStream src = File.OpenRead(srcName);
			using FileStream br = File.OpenRead(srcName + ".br");

			using RentBuffer data = new();
			data.Reset(8192);
			Memory<byte> compressed = data.Memory[..br.Read(data.Span)];
			ReadOnlySequence<byte> seq = new(compressed);
			seq.DecompressBrotli();
			StreamHelpers.AreEqual(src, new ReadOnlySequenceStream(ref seq));
		}
		
		[DataRow("data/BDanmakuProtocol.cs")]
		[TestMethod]
		public void BrokenTest(string srcName)
		{
			using FileStream br = File.OpenRead(srcName + ".broken.br");
			using RentBuffer data = new();
			data.Reset(8192);
			Memory<byte> compressed = data.Memory[..br.Read(data.Span)];
			ReadOnlySequence<byte> seq = new(compressed);
			Assert.ThrowsException<InvalidDataException>(() => seq.DecompressBrotli());
		}

		[DataRow("data/BDanmakuProtocol.cs")]
		[DataRow("data/BufferExtensions.cs")]
		[DataRow("data/Handshake2.cs")]
		[DataRow("data/SimpleSegment.cs")]
		[TestMethod]
		public void MultipleSegmentTest(string srcName)
		{
			using FileStream src = File.OpenRead(srcName);
			using FileStream br = File.OpenRead(srcName + ".br");

			using RentBuffer data = new();
			data.Reset(8192);
			Memory<byte> compressed = data.Memory[..br.Read(data.Span)];
			int midpoint = compressed.Length / 2;
			SimpleSegment first = new (compressed[..midpoint], 0);
			SimpleSegment last = first.SetNext(compressed[midpoint..], midpoint);
			ReadOnlySequence<byte> seq = new(first, 0, last, last.Memory.Length);
			seq.DecompressBrotli();

			StreamHelpers.AreEqual(src, new ReadOnlySequenceStream(ref seq));
		}
	}

	[TestClass()]
	public class DeflateTests
	{
		[TestInitialize]
		public void PrepareEnvironment()
		{
			Environment.CurrentDirectory = Path.GetDirectoryName(typeof(BrotliTests).Assembly.Location)!;
		}

		[DataRow("data/BDanmakuProtocol.cs")]
		[DataRow("data/BufferExtensions.cs")]
		[DataRow("data/Handshake2.cs")]
		[DataRow("data/SimpleSegment.cs")]
		[TestMethod()]
		public void DecompressTest(string srcName)
		{
			using FileStream src = File.OpenRead(srcName);
			using FileStream br = File.OpenRead(srcName + ".deflate");

			using RentBuffer data = new();
			data.Reset(8192);
			Memory<byte> compressed = data.Memory[..br.Read(data.Span)];
			ReadOnlySequence<byte> seq = new(compressed);
			seq.DecompressDeflate();
			StreamHelpers.AreEqual(src, new ReadOnlySequenceStream(ref seq));
		}

		[DataRow("data/BDanmakuProtocol.cs")]
		[TestMethod]
		public void BrokenTest(string srcName)
		{
			using FileStream br = File.OpenRead(srcName + ".broken.deflate");
			using RentBuffer data = new();
			data.Reset(8192);
			Memory<byte> compressed = data.Memory[..br.Read(data.Span)];
			ReadOnlySequence<byte> seq = new(compressed);
			Assert.ThrowsException<InvalidDataException>(() => seq.DecompressDeflate());
		}

		[DataRow("data/BDanmakuProtocol.cs")]
		[DataRow("data/BufferExtensions.cs")]
		[DataRow("data/Handshake2.cs")]
		[DataRow("data/SimpleSegment.cs")]
		[TestMethod]
		public void MultipleSegmentTest(string srcName)
		{
			using FileStream src = File.OpenRead(srcName);
			using FileStream br = File.OpenRead(srcName + ".deflate");

			using RentBuffer data = new();
			data.Reset(8192);
			Memory<byte> compressed = data.Memory[..br.Read(data.Span)];
			int midpoint = compressed.Length / 2;
			SimpleSegment first = new(compressed[..midpoint], 0);
			SimpleSegment last = first.SetNext(compressed[midpoint..], midpoint);
			ReadOnlySequence<byte> seq = new(first, 0, last, last.Memory.Length);
			seq.DecompressDeflate();

			StreamHelpers.AreEqual(src, new ReadOnlySequenceStream(ref seq));
		}

	}
}