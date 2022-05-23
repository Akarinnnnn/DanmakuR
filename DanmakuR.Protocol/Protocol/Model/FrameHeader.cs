using System.Buffers;
using System.Runtime.CompilerServices;

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

internal struct FrameHeader
{
	public FrameHeader(int frameLength, short headerLength, short version, int opCode, int seqId)
	{
		FrameLength = frameLength;
		HeaderLength = headerLength;
		_version = version;
		_opcode = opCode;
		SequenceId = seqId;
	}

	public int FrameLength;
	public short HeaderLength = 16;
	public short _version;
	public int _opcode;
	public int SequenceId = 1;


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