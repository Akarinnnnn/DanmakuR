using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DanmakuR.Protocol.Model;

internal enum FrameVersion : short
{
	Json,
	Int32BE,
	Deflate,
	Brotli
}

internal enum OpCode : int
{
	Invalid = 0,
	ClDefault = 1,
	Ping = 2,
	Pong = 3,
	Message = 5,
	ConnectAndAuth = 7,
	Connected = 8
}

// 按顺序排，序列化要用
[StructLayout(LayoutKind.Sequential, Size = 16)]
internal struct FrameHeader
{
	private const int ClDefaultSequence = Constants.WS_HEADER_DEFAULT_SEQUENCE;

	public FrameHeader()
	{

	}

	public int FrameLength = 0;
	public short HeaderLength = 16;
	public short _version = 0;
	public int _opcode = 0;
	public int SequenceId = ClDefaultSequence;


	public OpCode OpCode
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get => (OpCode)_opcode;
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		set => _opcode = (int)value;
	}
	public FrameVersion Version
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get => (FrameVersion)_version;
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		set => _version = (short)value;
	}

}