using DanmakuR;
using DanmakuR.BLiveClient;
using DanmakuR.Connection.Kestrel;
using DanmakuR.Protocol;
using DanmakuR.Protocol.Model;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using System.Net;
using static DanmakuR.BLiveClient.Configuration;

CancellationTokenSource closing = new();

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddCommandLine(args, new Dictionary<string, string>
{
	{ "-ps", SectionName + ":PseudoServer" },
	{ "-rid", SectionName + ":RoomId" },
	{ "-iep", SectionName + ":IPEndPoint" },
	{ "-url", SectionName + ":UrlEndpoint" },
	{ "-s", SectionName + ":ShortId" },
	{ "-v", SectionName + ":MaxVersion" }
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
	x.AcceptedPacketType = cfg.MaxVersion switch
	{
		0 => FrameVersion.Json,
		2 => FrameVersion.Deflate,
		_ => FrameVersion.Brotli
	};
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
if (cfg.PseudoServer)
{
	connBuilder.WithAutomaticReconnect();
}
var connection = connBuilder.Build();
Console.CancelKeyPress += async (_, eargs) =>
{
	Console.WriteLine("已按下退出键，正在断开连接");
	await connection.StopAsync().ConfigureAwait(false);
	Console.WriteLine("已断开连接");
	closing.Cancel();
};
Listener listener = new(app.Services.GetRequiredService<ILogger<Listener>>(), cfg.RoomId);

// connection.BindListeners(listener);
listener.BindToConnection(connection);

if (!cfg.PseudoServer)
{
	connection.HandshakeTimeout = TimeSpan.FromMinutes(10);
	connection.ServerTimeout = TimeSpan.FromMinutes(30);
	connection.KeepAliveInterval = TimeSpan.FromSeconds(40);
}
else
{
	connection.HandshakeTimeout = TimeSpan.FromMinutes(6);
	connection.ServerTimeout = TimeSpan.FromMinutes(60);
	connection.KeepAliveInterval = TimeSpan.FromSeconds(20);

}

await connection.StartAsync();
HubConnectionState state;
CancellationToken ct = closing.Token;

await app.RunAsync(ct); // 取消框架任务

while ((state = connection.State) != HubConnectionState.Disconnected)
{
	ct.ThrowIfCancellationRequested();
	await Task.Delay(5000).ConfigureAwait(false);
	app.Logger.LogInformation("连接状态: {state}", state);
}