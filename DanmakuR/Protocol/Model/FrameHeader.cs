using System.Buffers;

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
	Ping = 2,
	Pong = 3,
	Message = 5,
	ConnectAndAuth = 7,
	Connected = 8
}

internal struct FrameHeader
{
	public FrameHeader(int frameLength, short headerLength, short version, int opCode, int unknownField)
	{
		FrameLength = frameLength;
		HeaderLength = headerLength;
		_version = version;
		_opcode = opCode;
		UnknownField = unknownField;
	}

	public int FrameLength;
	public short HeaderLength = 16;
	public short _version;
	public int _opcode;
	public int UnknownField = 1;

	public OpCode OpCode { get => (OpCode)_opcode; set => _opcode = (int)value; }
	public FrameVersion Version { get => (FrameVersion)_version; set => _version = (short)value; }

}