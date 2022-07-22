using DanmakuR;
using DanmakuR.BLiveClient;
using DanmakuR.Protocol.Model;
using Microsoft.AspNetCore.SignalR.Client;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

var connBuilder = new BLiveHubConnectionBuilder();
connBuilder.UseSocketTransport(_ => { });
connBuilder.WithRoomid(1, x =>
{
	x.AcceptedPacketType = FrameVersion.Deflate;
	x.Platform = $".NET {typeof(object).Assembly.ImageRuntimeVersion}";
});
var connection = connBuilder.Build();
Listener listener = new(app.Services.GetRequiredService<ILogger<Listener>>(), 1);
connection.BindListeners(listener);
// connection.HandshakeTimeout = TimeSpan.FromSeconds(5);
connection.KeepAliveInterval = TimeSpan.FromSeconds(35);
Console.CancelKeyPress += async (sender, eargs) =>
{
	Console.WriteLine("�Ѱ����˳��������ڶϿ�����");
	await connection.DisposeAsync();
};

await connection.StartAsync();
HubConnectionState state;
while ((state = connection.State) != HubConnectionState.Disconnected)
{
	await Task.Delay(5000);
	app.Logger.LogInformation("����״̬: {state}", state);
}

Console.WriteLine("�ѶϿ�����");