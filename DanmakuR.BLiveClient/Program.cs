using DanmakuR;
using DanmakuR.BLiveClient;
using DanmakuR.Protocol.Model;
using Microsoft.AspNetCore.SignalR.Client;
using System.Net;

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddCommandLine(args, new Dictionary<string, string>
{
	{"-ps", "PseudoServer" },
	{ "--pseudoServer", "PseudoServer" }
});
var app = builder.Build();

var connBuilder = new BLiveHubConnectionBuilder();
connBuilder.UseSocketTransport(_ => { });
connBuilder.WithRoomid(1, x =>
{
	x.AcceptedPacketType = FrameVersion.Deflate;
	x.Platform = $".NET {typeof(object).Assembly.ImageRuntimeVersion}";
});

if (builder.Configuration.GetValue("PseudoServer", false))
	connBuilder.Services.AddSingleton<EndPoint, IPEndPoint>(_ => new IPEndPoint(IPAddress.Loopback, 2243));

var connection = connBuilder.Build();
Listener listener = new(app.Services.GetRequiredService<ILogger<Listener>>(), 1);
connection.BindListeners(listener);
// connection.HandshakeTimeout = TimeSpan.FromSeconds(5);
// connection.KeepAliveInterval = TimeSpan.FromSeconds(35);
Console.CancelKeyPress += async (sender, eargs) =>
{
	Console.WriteLine("已按下退出键，正在断开连接");
	await connection.DisposeAsync();
};

if (builder.Configuration.GetValue("PseudoServer", false))
{
	connection.HandshakeTimeout = TimeSpan.FromMinutes(5);
	connection.ServerTimeout = TimeSpan.FromMinutes(5);
	connection.KeepAliveInterval = TimeSpan.FromSeconds(30); 
}

await connection.StartAsync();
HubConnectionState state;
while ((state = connection.State) != HubConnectionState.Disconnected)
{
	await Task.Delay(5000);
	app.Logger.LogInformation("连接状态: {state}", state);
}

Console.WriteLine("已断开连接");