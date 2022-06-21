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
		private readonly SequencePosition endPosition;
		internal readonly OpCode opcode;

		/// <summary>
		/// 
		/// </summary>
		/// <param name="endPosition">消息末尾（<see cref="FrameHeader.FrameLength"/>）</param>
		/// <param name="opcode"></param>
		public MessagePackage(SequencePosition endPosition, OpCode opcode)
		{
			this.endPosition = endPosition;
			this.opcode = opcode;
		}

		public bool IsCompleted { get; private set; } = false;
		public SequencePosition End => endPosition;

		[MethodImpl(MethodImplOptions.NoInlining)]
		private void CheckDelimiter(ref SequencePosition pos, in ReadOnlySequence<byte> data)
		{
			if (data.IsSingleSegment)
			{
				int index = pos.GetInteger();
				byte nextByte = data.FirstSpan[index + 1];
				if (nextByte == '{')
					return;

				// 分隔符
				if (nextByte < 0x1e)
				{
					// 要不加个字段统计一下？
					pos = data.GetPosition(2, pos);
					return;
				}
				else
				{
					// 什么玩意？
					Complete(ref pos);
					return;
				}
			}

			var nextPos = pos;
			if (data.TryGet(ref nextPos, out var current))
			{
				if (current.IsEmpty && SkipEmptySegment(ref nextPos, in data, out current))
					// 剩下全空，噶了
					Complete(ref pos);

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

		private void Complete(ref SequencePosition pos)
		{
			IsCompleted = true;
			pos = endPosition;
		}

		/// <summary>
		/// 把position改到下一个json开始，或者数据包结尾
		/// </summary>
		/// <param name="position"><see cref="Utf8JsonReader.Position"/></param>
		/// <param name="data"></param>
		[MethodImpl(MethodImplOptions.AggressiveOptimization)]
		public void FitNextRecord(ref SequencePosition position, in ReadOnlySequence<byte> data)
		{
			if (position.Equals(endPosition))
			{
				IsCompleted = true;
				return;
			}

			CheckDelimiter(ref position, in data);
		}

		[MethodImpl(MethodImplOptions.AggressiveOptimization)]
		public Utf8JsonReader ReadOne(in ReadOnlySequence<byte> data)
		{
			if (data.IsEmpty)
				return new();

			return new(data);
		}
	}
}
