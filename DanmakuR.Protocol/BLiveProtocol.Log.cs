using Microsoft.Extensions.Logging;

namespace DanmakuR.Protocol;

public partial class BLiveProtocol
{
	private static partial class Log
	{
		//[LoggerMessage(1, LogLevel.Debug, "一次发了几个json，有点离谱", EventName = "MultipleJson")]
		//public static partial void MultipleMessage(ILogger logger);

		[LoggerMessage(2, LogLevel.Warning, "Json含有语法错误", EventName = "InvalidJson")]
		public static partial void InvalidJson(ILogger logger);

		[LoggerMessage(3, LogLevel.Error, "无法解析数据包", EventName = "InvalidData")]
		public static partial void InvalidData(ILogger logger);

		[LoggerMessage(4, LogLevel.Warning, "消息json最外层含有未知属性{propertyName}", EventName = "UnreconizedInvocationProperty")]
		public static partial void UnrecognizedInvocationProperty(ILogger logger, string propertyName);

		[LoggerMessage(5, LogLevel.Warning, "Json不符合格式", EventName = "NotAnInvocation")]
		public static partial void NotAnInvocation(ILogger logger);

	}
}
