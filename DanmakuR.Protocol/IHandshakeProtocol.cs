using Microsoft.AspNetCore.SignalR.Protocol;
using System.Buffers;
using System.Diagnostics.CodeAnalysis;

namespace DanmakuR.Protocol
{
	public interface IHandshakeProtocol
	{
		bool TryParseResponseMessage(ref ReadOnlySequence<byte> buffer, [NotNullWhen(true)] out HandshakeResponseMessage? responseMessage);
		void WriteRequestMessage(HandshakeRequestMessage requestMessage, IBufferWriter<byte> output);
	}
}