using DanmakuR.Resources;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace DanmakuR.Protocol.Model
{
	internal struct MessagePackage
	{
		private SequencePosition pos = default;
		private int index = 0;
		private int length = 0;
		internal readonly ReadOnlySequence<byte> data;
		internal readonly OpCode opcode;

		private static readonly byte[] delimiters =
		{
			0, 1, 2, 3, 4, 5, 6, 7, 8, 9,
			10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20,
			21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31
		};
		internal static ReadOnlySpan<byte> Delimiters => delimiters;

		/// <summary>
		/// 
		/// </summary>
		/// <param name="data">不含消息头</param>
		/// <param name="opcode"></param>
		public MessagePackage(in ReadOnlySequence<byte> data, OpCode opcode)
		{
			this.data = data;
			this.opcode = opcode;
			
			if (!data.IsSingleSegment)
				pos = data.Start;
			else
				length = unchecked((int)data.Length);
		}

		/// <summary>
		/// 用完记得加上去
		/// </summary>
		/// <param name="pos"></param>
		public bool IsEmpty
		{
			[MethodImpl(MethodImplOptions.AggressiveOptimization)]
			get
			{
				if (length != 0)
					return index > length;
				else 
					return pos.Equals(data.End);
			}
		}
		[MethodImpl(MethodImplOptions.AggressiveOptimization)]
		public Utf8JsonReader ReadOne()
		{
			if (length != 0)
			{
				var span = data.FirstSpan;
				var endIndex = span.IndexOf(Delimiters);
				if (endIndex == -1)
					throw new InvalidOperationException(SR.Sequence_ReachedEnd);
				var ret = new Utf8JsonReader(span.Slice(index, endIndex));
				index = endIndex + 1;

				return ret;
			}
			else
			{

			}
		}
	}
}
