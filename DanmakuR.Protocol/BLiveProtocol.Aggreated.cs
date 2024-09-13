namespace DanmakuR.Protocol;

public partial class BLiveProtocol
{
	public static async Task HandleAggreatedMessages(ParsingAggregatedMessageState state)
	{
		var input = state.Buffer;
		var writer = state.Writer;
		var protocol = state.HubProtocol;
		var binder = state.Binder;
		using (state)
		{
			while (!input.IsEmpty && protocol.ParseMessageCore( binder, out var message, ref input))
			{
				await writer.WriteAsync(message);
			}
		}
	}
}