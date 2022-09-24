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
			while (!input.IsEmpty && protocol.TryParseMessage(ref input, binder, out var message))
			{
				await writer.WriteAsync(message);
			}
		}
	}
}