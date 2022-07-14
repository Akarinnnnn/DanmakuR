namespace DanmakuR.Protocol;

public partial class BLiveProtocol
{
	public static async Task HandleAggreatedMessages(ParsingAggreatedMessageState state)
	{
		var input = state.Buffer;
		var writer = state.Writer;
		var protocol = state.HubProtocol;
		var binder = state.Binder;
		using (state)
		{
			if (state.IsSequentialFrames)
			{
				while (!input.IsEmpty && protocol.TryParseMessage(ref input, binder, out var message))
				{
					await writer.WriteAsync(message);
				}
			}
			else
			{
				MessagePackage package = new (input, Model.OpCode.Message);
				while (!package.IsCompleted)
				{
					var pos = protocol.ParseInvocation(package.ReadOne(), binder, out var message);
					package.FitNextRecord(pos);
					await writer.WriteAsync(message);
				}
			}
		}
	}
}