using System.Runtime.CompilerServices;

namespace DanmakuR.Protocol
{
	[Flags]
	public enum TransportTypes
	{
		Unspecified,
		InsecureWebsocket = 1,
		SecureWebsocket = 2,
		RawSocket = 4,
		Websocket = InsecureWebsocket | SecureWebsocket
	}

	public static class TransportTypesExtension
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool HasRawSocket(this TransportTypes val)
		{
			return (val & TransportTypes.RawSocket) != 0;
		}

		public static bool HasWebSocket(this TransportTypes val)
		{
			return (val & TransportTypes.Websocket) != 0;
		}
	}
}