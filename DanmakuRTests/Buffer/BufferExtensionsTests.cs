extern alias protocol;

using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Buffers;
using protocol::DanmakuR.Protocol.Buffer;
using protocol::DanmakuR.Protocol;

namespace DanmakuRTests.Buffer
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
				if (readE != readA)
					throw new AssertFailedException($"read: {readE} != {readA}.");

				if (!buffE.Span[0..readE].SequenceEqual(buffA.Span[0..readA]))
					throw new AssertFailedException($"Difference between [{excepted.Length}..{excepted.Length + readE}].");
			}
		}
	}
}