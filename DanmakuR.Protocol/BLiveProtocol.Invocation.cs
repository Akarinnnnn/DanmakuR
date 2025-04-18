﻿using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Protocol;
using System.Runtime.ExceptionServices;
using System.Text.Json;
using static DanmakuR.Protocol.InvocationJsonHelper;
using static DanmakuR.Protocol.WellKnownMethods;

namespace DanmakuR.Protocol;

internal static class InvocationJsonHelper
{
	internal const string NameCmd = "cmd";
	internal static JsonEncodedText TextCmd = JsonEncodedText.Encode(NameCmd);

	internal const string NameData = "data";
	internal static JsonEncodedText TextData = JsonEncodedText.Encode(NameData);
}


partial class BLiveProtocol
{
	/// <devdoc>
	/// <summary>
	/// 解析<see cref="OpCode.Message"/>数据包中的Json信息
	/// </summary>
	/// <returns>单条json的最后一个标记位置</returns>
	/// <remarks>
	/// 先尝试绑定到已注册cmd的处理器，若未注册则转交给OnMessageJdonDocument，均未注册则抛出异常
	/// </remarks>
	/// </devdoc>
	private SequencePosition ParseInvocation(Utf8JsonReader reader, IInvocationBinder binder, out HubMessage msg)
	{
		string? cmdName = null;
		try
		{
			//
			JsonDocument fullData = JsonDocument.ParseValue(ref reader);
			cmdName = fullData.RootElement.GetProperty(TextCmd.EncodedUtf8Bytes).GetString()
					?? throw new InvalidDataException("cmd为空");

			IReadOnlyList<Type> cmdHandlerArgs = binder.GetParameterTypes(cmdName);

			if (cmdHandlerArgs.Count == 1) // 已注册对应cmd的处理器
			{
				using (fullData)
				{
					msg = new InvocationMessage(cmdName, [
						fullData.Deserialize(cmdHandlerArgs[0], optionsMonitor.CurrentValue.SerializerOptions)
					]);
				}
			}
			else if (cmdHandlerArgs.Count == 0) // 未注册，转交给默认处理器
			{
				AssertMethodParamTypes(binder, OnMessageJsonDocument.Name, OnMessageJsonDocument.ReadonlyParamTypes);
				msg = new InvocationMessage(OnMessageJsonDocument.Name, [cmdName, fullData]);
			}
			else if (cmdHandlerArgs.Count > 1)
			{
				throw new NotSupportedException("事件处理器只能使用一个方法参数接收事件");
			}
			else
			{
				throw new InvalidDataException("消息缺少cmd或其它问题");
			}
		}
		catch (Exception ex)
		{
			msg = new InvocationBindingFailureMessage(null, cmdName ?? OnMessageJsonDocument.Name, ExceptionDispatchInfo.Capture(ex));
			return default;
		}

		return reader.Position;
	}
}