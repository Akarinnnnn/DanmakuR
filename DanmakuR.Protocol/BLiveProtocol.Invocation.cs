using static DanmakuR.Protocol.WellKnownMethods;
using static DanmakuR.Protocol.InvocationJsonHelper;

using System.Diagnostics.CodeAnalysis;
using System.Runtime.ExceptionServices;
using System.Text.Json;
using Microsoft.AspNetCore.Internal;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Protocol;


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
	/// </devdoc>
	private SequencePosition ParseInvocation(Utf8JsonReader reader, IInvocationBinder binder, out HubMessage msg)
	{
		try
		{
			AssertMethodParamTypes(binder, ProtocolOnAggreatedMessage.Name, ProtocolOnAggreatedMessage.ParamTypes);
			reader.CheckRead();
			reader.EnsureObjectStart();
			JsonDocument? fullData = null;
			string? cmdName = null;
			fullData = JsonDocument.ParseValue(ref reader);

			try
			{
				cmdName = fullData.RootElement.GetProperty("cmd").GetString();
			}
			catch (KeyNotFoundException ex)
			{
				throw new InvalidDataException("缺少cmd属性", ex);
			}

			if (cmdName == null)
			{
				throw new InvalidDataException("缺少cmd属性");
			}


			msg = new InvocationMessage(OnMessageJsonDocument.Name, new object[] { cmdName, fullData });
		}
		catch (BindingFailureException ex)
		{
			msg = new InvocationBindingFailureMessage(null, ProtocolOnAggreatedMessage.Name, ExceptionDispatchInfo.Capture(ex));
		}
		return reader.Position;
	}
}