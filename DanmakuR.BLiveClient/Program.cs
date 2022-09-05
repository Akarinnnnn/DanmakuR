using DanmakuR;
using DanmakuR.BLiveClient;
using DanmakuR.Connection.Kestrel;
using DanmakuR.Protocol;
using DanmakuR.Protocol.Model;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using System.Net;
using static DanmakuR.BLiveClient.Configuration;
var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddCommandLine(args, new Dictionary<string, string>
{
	{ "-ps", SectionName + ":PseudoServer" },
	{ "-rid", SectionName + ":RoomId" },
	{ "-iep", SectionName + ":IPEndPoint" },
	{ "-s", SectionName + ":ShortId" }
});
var app = builder.Build();

var cfg = new Configuration();
app.Configuration.Bind(SectionName, cfg);

app.Logger.LogInformation("房号：{roomid}", cfg.RoomId);
var connBuilder = new BLiveHubConnectionBuilder();

connBuilder.WithRoomid(cfg.RoomId, x =>
{
	x.AcceptedPacketType = FrameVersion.Brotli;
	x.Platform = $".NET";
});

connBuilder.Services.Configure<BLiveOptions>(o => o.MightBeShortId = cfg.ShortId);

if (cfg.PseudoServer)
{
	connBuilder.Services.AddKestrelClientSocket()
		.AddSingleton<EndPoint, IPEndPoint>(_ => new IPEndPoint(IPAddress.Loopback, 2243));
}
else if (cfg.IPEndPoint != null)
{
	connBuilder.Services.AddKestrelClientSocket()
		.AddSingleton<EndPoint, IPEndPoint>(_ => cfg.IPEndPoint);
}
else if (cfg.UriEndPoint != null)
{
	connBuilder.UseWebsocketTransport(o =>
	{
		o.Url = cfg.UriEndPoint.Uri;
	}, cfg.UriEndPoint.Uri.Scheme == Uri.UriSchemeWss)
		.Services
		.AddSingleton<EndPoint, UriEndPoint>(_ => cfg.UriEndPoint);
}
else
{
	connBuilder.Services.AddKestrelClientSocket();
}

var connection = connBuilder.Build();
Listener listener = new(app.Services.GetRequiredService<ILogger<Listener>>(), cfg.RoomId);

// connection.BindListeners(listener);
listener.BindToConnection(connection);

CancellationTokenSource closing = new();
Console.CancelKeyPress += (_, _) =>
{
	closing.Cancel();
	Environment.Exit(0);// 搞不懂，不加这行不会退出
};
if (builder.Configuration.GetValue("PseudoServer", false))
{
	connection.HandshakeTimeout = TimeSpan.FromMinutes(5);
	connection.ServerTimeout = TimeSpan.FromMinutes(5);
	connection.KeepAliveInterval = TimeSpan.FromSeconds(30);
}

await connection.StartAsync();
HubConnectionState state;
CancellationToken ct = closing.Token;
while ((state = connection.State) != HubConnectionState.Disconnected)
{
	await Task.Delay(5000).ConfigureAwait(false);
	app.Logger.LogInformation("连接状态: {state}", state);
	ct.ThrowIfCancellationRequested();
}

Console.WriteLine("已按下退出键，正在断开连接");
closing.Cancel();
closing.Dispose();
await connection.StopAsync().ConfigureAwait(false);

Console.WriteLine("已断开连接");
