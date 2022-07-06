using DanmakuR.Protocol.Model;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace DanmakuR.Protocol
{
	internal struct MessagePackage
	{
		private SequencePosition end;
		private ReadOnlySequence<byte> sequence;
		internal readonly OpCode opcode;

		/// <summary>
		/// 
		/// </summary>
		/// <param name="endPosition">消息末尾（<see cref="FrameHeader.FrameLength"/>）</param>
		/// <param name="opcode"></param>
		public MessagePackage(ReadOnlySequence<byte> sequence, OpCode opcode)
		{
			this.sequence = sequence;
			end = this.sequence.End;
			this.opcode = opcode;
		}

		public bool IsCompleted 
		{ 
			[method:MethodImpl(MethodImplOptions.AggressiveInlining)] get;
			private set; 
		} = false;
		public SequencePosition End => sequence.End;

		[MethodImpl(MethodImplOptions.NoInlining)]
		private void CheckDelimiter(SequencePosition pos)
		{
			if (sequence.IsSingleSegment)
			{
				int index = pos.GetInteger();
				byte nextByte = sequence.FirstSpan[index + 1];
				if (nextByte == '{')
					return;

				// 分隔符
				if (nextByte < 0x1e)
				{
					// 要不加个字段统计一下？
					sequence = sequence.Slice(index + 1);
					return;
				}
				else
				{
					// 下一个数据包
					IsCompleted = true;
					return;
				}
			}

			var nextPos = sequence.GetPosition(1, pos);
			if (sequence.TryGet(ref nextPos, out var current))
			{
				if (current.IsEmpty && SkipEmptySegment(ref nextPos, in sequence, out current))
					// 剩下全空
					IsCompleted = true;

				if (current.Span[0] < 0x1e)
					sequence.Slice(sequence.GetPosition(1, nextPos));
			}
		}

		// 一种边界情况，下面给张灵魂作画解释一下
		//   序列1 ->   序列2
		// ..., n ]   [ 0, 1, ...
		//   pos^
		// pos指在前一个序列的最后一个元素。
		// 这时候TryGet拿到的Memory就是空的，返回还是true
		/// <returns><see langword="true"/>剩下全空，噶了；<see langword="false"/>继续</returns>
		/// <summary>返回<see langword="false"/>时，memory保证有至少一个元素</summary>
		[MethodImpl(MethodImplOptions.NoInlining)]
		private static bool SkipEmptySegment(ref SequencePosition position, in ReadOnlySequence<byte> data, out ReadOnlyMemory<byte> memory)
		{
			while (data.TryGet(ref position, out memory) && memory.IsEmpty) ;
			return memory.IsEmpty;
		}

		/// <summary>
		/// 把position改到下一个json开始，或者数据包结尾
		/// </summary>
		/// <param name="position"><see cref="Utf8JsonReader.Position"/></param>
		/// <param name="data"></param>
		[MethodImpl(MethodImplOptions.AggressiveOptimization)]
		public void FitNextRecord(SequencePosition position)
		{
			if (position.Equals(sequence.End))
			{
				IsCompleted = true;
				return;
			}

			CheckDelimiter(position);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public Utf8JsonReader ReadOne()
		{
			Debug.Assert(!sequence.IsEmpty);
			
			return new(sequence);
		}
	}
}
