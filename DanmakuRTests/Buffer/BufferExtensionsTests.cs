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
		public static bool AreEqual(Stream excepted, Stream actual)
		{
			// ArrayPool<byte>.Shared.Rent
			using RentBuffer buffE = new();
			using RentBuffer buffA = new();
			buffE.Reset(8192);
			buffA.Reset(8192);
			int readE = 0, readA = 0;
			while ((readE = excepted.Read(buffE.Span)) != 0 && (readA = actual.Read(buffA.Span)) != 0)
			{
				if(readE != readA)
					return false;

				if(!MemoryExtensions.SequenceEqual(buffE.Span[0..readE], buffA.Span[0..readA]))
					return false;
			}
			return true;
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
			Assert.IsTrue(StreamHelpers.AreEqual(src, new ReadOnlySequenceStream(ref seq)), "内容");
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

			Assert.IsTrue(StreamHelpers.AreEqual(src, new ReadOnlySequenceStream(ref seq)), "内容");
		}
	}
}