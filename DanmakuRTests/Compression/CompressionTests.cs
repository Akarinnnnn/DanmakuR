extern alias protocol;

using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;

using protocol::DanmakuR.Protocol;
using System.IO.Pipelines;

namespace DanmakuRTests.Compression
{
	[TestClass]
	public class CompressionTests
	{
		[TestInitialize]
		public void PrepareEnvironment()
		{
			Environment.CurrentDirectory = Path.GetDirectoryName(typeof(CompressionTests).Assembly.Location)!;
		}

		[DataRow("data/BDanmakuProtocol.cs")]
		[DataRow("data/BufferExtensions.cs")]
		[DataRow("data/Handshake2.cs")]
		[DataRow("data/SimpleSegment.cs")]
		[TestMethod]
		public void TestBrotli(string filename)
		{
			ArrayBufferWriter<byte> writer = new(5555);
			byte[] src = File.ReadAllBytes(filename);
			byte[] br = File.ReadAllBytes(filename + ".br");

			ReadOnlySequence<byte> seq = new(br);
			seq.DecompressBrotli(writer);
		
			Assert.IsTrue(src.AsSpan().SequenceEqual(writer.WrittenSpan));
		}

		[DataRow("data/BDanmakuProtocol.cs")]
		[TestMethod]
		public void TestBrotliBroken(string filename)
		{
			Assert.ThrowsException<InvalidDataException>(() =>
			{
				ReadOnlySequence<byte> broken = new(File.ReadAllBytes(filename));
				broken.DecompressBrotli(new ArrayBufferWriter<byte>(5555));
			});
		}

		private sealed class Segment : ReadOnlySequenceSegment<byte>
		{
			private readonly ReadOnlyMemory<byte> _buffer;
			private readonly int _end;
			private Segment? _next;

			public int End => _end;

			public Segment(ReadOnlyMemory<byte> buffer)
			{
				_buffer = buffer;
				_end = buffer.Length;

				Memory = buffer;
			}

			public Segment? NextSegment
			{
				get => _next;
				set
				{
					_next = value;
					Next = value;
				}
			}

			public Segment CreateNext(ReadOnlyMemory<byte> buffer)
			{
				NextSegment = new(buffer);
				NextSegment.RunningIndex += _end;

				return NextSegment;
			}
		}

		[DataRow("data/BDanmakuProtocol.cs")]
		[DataRow("data/BufferExtensions.cs")]
		[DataRow("data/Handshake2.cs")]
		[DataRow("data/SimpleSegment.cs")]
		[TestMethod]
		public void TestBrMultipleSegment(string filename)
		{
			byte[] src = File.ReadAllBytes(filename);
			byte[] br = File.ReadAllBytes(filename + ".br");
			ArrayBufferWriter<byte> writer = new(2000);


			Segment first = new(br.AsMemory(0, br.Length / 2));
			Segment last = first.CreateNext(br.AsMemory(br.Length / 2));

			ReadOnlySequence<byte> seq = new(first, 0, last, last.Memory.Length);
			seq.DecompressBrotli(writer);

			Assert.IsTrue(src.AsSpan().SequenceEqual(writer.WrittenSpan));
		}
	}
}
