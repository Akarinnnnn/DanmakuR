namespace DanmakuR.Protocol
{
	internal static class Constants
	{
		internal const int WS_OP_HEARTBEAT = 2;
		internal const int WS_OP_HEARTBEAT_REPLY = 3;
		internal const int WS_OP_MESSAGE = 5;
		internal const int WS_OP_USER_AUTHENTICATION = 7;
		internal const int WS_OP_CONNECT_SUCCESS = 8;
		internal const int WS_PACKAGE_HEADER_TOTAL_LENGTH = 16;
		internal const int WS_PACKAGE_OFFSET = 0;
		internal const int WS_HEADER_OFFSET = 4;
		internal const int WS_VERSION_OFFSET = 6;
		internal const int WS_OPERATION_OFFSET = 8;
		internal const int WS_SEQUENCE_OFFSET = 12;
		internal const int WS_BODY_PROTOCOL_VERSION_NORMAL = 0;
		internal const int WS_BODY_PROTOCOL_VERSION_BROTLI = 3;
		internal const int WS_HEADER_DEFAULT_VERSION = 1;
		internal const int WS_HEADER_DEFAULT_OPERATION = 1;
		internal const int WS_HEADER_DEFAULT_SEQUENCE = 1;
		internal const int WS_AUTH_OK = 0;
		internal const int WS_AUTH_TOKEN_ERROR = -101;
	}
}
