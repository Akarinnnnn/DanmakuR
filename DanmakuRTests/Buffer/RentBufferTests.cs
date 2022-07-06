extern alias protocol;

using Microsoft.VisualStudio.TestTools.UnitTesting;
using protocol::DanmakuR.Protocol.Buffer;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DanmakuRTests.Buffer
{
	[TestClass]
	public class RentBufferTests
	{
		[TestMethod]
		public void ResetLargerWithCopyTest()
		{
			byte[] excepted = new byte[16];
			for (int i = 0; i < excepted.Length; i++)
				excepted[i] = unchecked((byte)i);
			RentBuffer buffer = new();
			buffer.Reset(16);
			excepted.AsSpan().CopyTo(buffer.Span);
			buffer.Reset(32, true);

			Assert.IsTrue(excepted.AsSpan().SequenceEqual(buffer.Span[..16]));
		}

		[TestMethod]
		public void ResetShorterWithCopyTest()
		{
			byte[] excepted = new byte[32];
			for (int i = 0; i < excepted.Length; i++)
				excepted[i] = unchecked((byte)i);
			RentBuffer buffer = new();
			buffer.Reset(32);
			excepted.AsSpan().CopyTo(buffer.Span);
			buffer.Reset(16, true);

			Assert.IsTrue(excepted.AsSpan()[..16].SequenceEqual(buffer.Span));
		}

		[TestMethod]
		public void NullDisposeTest()
		{
			RentBuffer buffer = new ();
			buffer.Dispose();
		}
	}
}
