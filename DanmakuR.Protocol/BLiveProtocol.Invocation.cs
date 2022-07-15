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
		//TODO: 解析json
		try
		{
			AssertMethodParamTypes(binder, ProtocolOnAggreatedMessage.Name, ProtocolOnAggreatedMessage.ParamTypes);
			reader.EnsureObjectStart();
			JsonDocument? dataBody = null;
			string? cmdName = null;
			while (reader.Read())
			{
				if (reader.TokenType == JsonTokenType.PropertyName)
				{
					if (reader.ValueTextEquals(TextCmd.EncodedUtf8Bytes))
					{
						cmdName = reader.ReadAsString(NameCmd);
						break;
					}
					else if (reader.ValueTextEquals(TextData.EncodedUtf8Bytes))
					{
						reader.EnsureObjectStart();
						dataBody = JsonDocument.ParseValue(ref reader);
					}
					else
					{
						Log.UnreconizedInvocationProperty(logger, reader.GetString()!);
						reader.Skip();
					}
				}
			}

			if(cmdName == null)
			{
				throw new InvalidDataException("缺少cmd属性");
			}

			if(dataBody == null)
			{
				throw new InvalidDataException("缺少data属性");
			}

			msg = new InvocationMessage(OnMessageJsonDocument.Name, new object[] { cmdName, dataBody });
		}
		catch (BindingFailureException ex)
		{
			msg = new InvocationBindingFailureMessage(null, ProtocolOnAggreatedMessage.Name, ExceptionDispatchInfo.Capture(ex));

		}
		return reader.Position;
	}
}