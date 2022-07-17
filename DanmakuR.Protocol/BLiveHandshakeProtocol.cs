using DanmakuR.Protocol.Buffer;
using DanmakuR.Protocol.Model;
using DanmakuR.Protocol.Resources;
using Microsoft.AspNetCore.Internal;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Protocol;
using Microsoft.Extensions.Options;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using static DanmakuR.Protocol.BLiveMessageParser;


namespace DanmakuR.Protocol;

public class BLiveHandshakeProtocol : IHandshakeProtocol
{
	private readonly Handshake2 hs2;

	public BLiveHandshakeProtocol(IOptions<Handshake2> hs2)
	{
		this.hs2 = hs2.Value;
	}

	public bool TryParseResponseMessage(ref ReadOnlySequence<byte> buffer, [NotNullWhen(true)] out HandshakeResponseMessage? responseMessage)
	{
		if (!TrySliceInput(in buffer, out var response, out _))
		{
			responseMessage = null;
			return false;
		}
		// 同一个Sequence切下来的就可以这么搞
		buffer = buffer.Slice(response.End);
		try
		{
			if (HandshakeResponse.IsTemplateSuccessful(in response))
			{
				responseMessage = new HandshakeResponseMessage(null);
				return true;
			}

			var code = HandshakeResponse.ParseResponse(new(response));
			if (code == 0)
			{
				responseMessage = new HandshakeResponseMessage(null);
				return true;
			}
			else if (code == Constants.WS_AUTH_TOKEN_ERROR)
			{
				responseMessage = new HandshakeResponseMessage("WS_AUTH_TOKEN_ERROR");
				return true;
			}
			else
			{
				responseMessage = new(string.Format("握手失败，错误代码：{0}", code));
				return true;
			}
		}
		catch (InvalidDataException ex)
		{
			responseMessage = new HandshakeResponseMessage(ex.Message);
			return true;
		}
	}

	public void WriteRequestMessage(HandshakeRequestMessage requestMessage, IBufferWriter<byte> output)
	{
		MemoryBufferWriter writer = MemoryBufferWriter.Get();
		FrameHeader header = new();
		try
		{
			WriteRequestMessageCore(requestMessage, writer, ref header);
			header.WriteToOutput(output);
			writer.CopyTo(output);
		}
		finally
		{
			MemoryBufferWriter.Return(writer);
		}
	}

	private void WriteRequestMessageCore(HandshakeRequestMessage m, MemoryBufferWriter temp, ref FrameHeader header)
	{

		if (m.Protocol == ProtocolName && m.Version == SupportedProtocolVersion)
		{
			hs2.Serialize(temp);
			header.OpCode = OpCode.ConnectAndAuth;
			header.FrameLength = unchecked((int)(header.HeaderLength + temp.Length));
		}
		else
		{
			throw new HubException(string.Format(SR.Protocol_Mismatch,
				m.Protocol,
				m.Version.ToString(),
				ProtocolName,
				SupportedProtocolVersion.ToString()
			));
		}
	}
}
