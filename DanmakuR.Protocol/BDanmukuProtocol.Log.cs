using Microsoft.Extensions.Logging;

namespace DanmakuR.Protocol;

public partial class BDanmakuProtocol
{
	private static partial class Log
	{
		[LoggerMessage(1, LogLevel.Debug, "一次发了几个json，有点离谱", EventName = "MultipleJson")]
		public static partial void MultipleMessage(ILogger logger);

		[LoggerMessage(2, LogLevel.Warning, "json有问题", EventName = "InvalidJson")]
		public static partial void InvalidJson(ILogger logger);

		[LoggerMessage(3, LogLevel.Error, "数据包有大问题", EventName = "InvalidData")]
		public static partial void InvalidData(ILogger logger);
	}
}
