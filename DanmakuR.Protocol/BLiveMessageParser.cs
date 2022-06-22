using DanmakuR.Protocol.Buffer;
using DanmakuR.Protocol.Model;
using System.Buffers;
using System.Runtime.CompilerServices;

namespace DanmakuR.Protocol
{
	internal static class BLiveMessageParser
	{
		internal const int SupportedProtocolVersion = Constants.WS_BODY_PROTOCOL_VERSION_BROTLI;
		// int32 framelength, int16 headerlength, int16 version, int32 opcode, int32 seqid
		private static readonly byte[] ping_message =
		{
			0, 0, 0, 16,
			0, 16,
			0, 1,
			0, 0, 0, 2,
			0, 0, 0, 1
		};

		internal static readonly string ProtocolName = "blive";
		internal static ReadOnlyMemory<byte> PingMessageMemory
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => ping_message;
		}
		
		internal static ReadOnlySpan<byte> PingMessageSpan
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => ping_message;
		}

		/// <summary>
		/// 从<paramref name="input"/>切出一个数据包
		/// </summary>
		/// <param name="input">待解析的数据流</param>
		/// <param name="payload">数据包内容</param>
		/// <param name="header"></param>
		/// <returns></returns>
		/// <remarks>单元测试需要internal</remarks>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		internal static bool TrySliceInput(in ReadOnlySequence<byte> input, out ReadOnlySequence<byte> payload, out FrameHeader header)
		{
			if (!(input.TryReadHeader(out header) && input.Length > header.FrameLength))
			{
				header = default;
				payload = default;
				return false;
			}

			payload = input.Slice(header.HeaderLength, header.FrameLength - header.HeaderLength);
			return true;
		}

		/// <summary>
		/// 从数据包中切出内容
		/// </summary>
		/// <param name="input">数据包，返回<see langword="true"/>时，修改为数据包内容</param>
		/// <param name="opcode"></param>
		/// <returns></returns>
		/// <remarks>单元测试需要internal</remarks>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		internal static bool TrySlicePayload(ref ReadOnlySequence<byte> input, out OpCode opcode)
		{
			if (!(input.TryReadHeader(out var header) && input.Length > header.FrameLength))
			{
				opcode = OpCode.Invalid;
				return false;
			}

			input = input.Slice(header.HeaderLength, header.FrameLength - header.HeaderLength);
			opcode = header.OpCode;
			return true;
		}
	}
}