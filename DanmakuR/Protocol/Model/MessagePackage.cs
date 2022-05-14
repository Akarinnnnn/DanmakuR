using DanmakuR.Buffer;
using DanmakuR.Resources;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
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
		private readonly int first_length;
		internal readonly ReadOnlySequence<byte> data;
		internal readonly OpCode opcode;
		private static readonly List<byte> got_delimiters = new(80);

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

			first_length = data.First.Length;
		}

		public bool IsEmpty
		{
			[MethodImpl(MethodImplOptions.AggressiveOptimization)]
			get
			{
				if (IsInFirst)
					return index >= first_length;
				else
					return pos.Equals(data.End);
			}
			[method: MethodImpl(MethodImplOptions.AggressiveInlining)]
			private set
			{
				Debug.Assert(value);
				index = first_length;
				pos = data.End;
			}
		}

		public bool IsInFirst
		{
			[method: MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => first_length > index;
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		private Utf8JsonReader ReadOneMultiSegment()
		{
			SequencePosition? result = data.FindDelimiterMultiSegment();
			if (result == null)
			{
				IsEmpty = true;
				return new(data);
			}

			var delimiter = data.Slice(result.Value, 1).FirstSpan[0];
			got_delimiters.Add(delimiter);
			result = data.GetPosition(1, result.Value); // result + 1
			return new(data.Slice(result.Value));
		}

		public void AdvanceTo(SequencePosition position)
		{
			pos = position;
		}

		[MethodImpl(MethodImplOptions.AggressiveOptimization)]
		public Utf8JsonReader ReadOne()
		{
			if (IsInFirst)
			{
				var span = data.FirstSpan;
				var endIndex = span.IndexOfAny(BufferExtensions.Delimiters);
				if (endIndex == -1)
				{
					if (data.IsSingleSegment)
					{
						IsEmpty = true;
						return new Utf8JsonReader(span);
					}
					else
					{
						return ReadOneMultiSegment();
					}
				}

				var ret = new Utf8JsonReader(span.Slice(index, endIndex));
				index = endIndex + 1;
				return ret;
			}
			else
			{
				return ReadOneMultiSegment();
			}
		}
	}
}
