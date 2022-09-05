using DanmakuR;
using DanmakuR.BLiveClient;
using DanmakuR.Connection.Kestrel;
using DanmakuR.Protocol.Model;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Configuration;
using System.Net;
using static DanmakuR.BLiveClient.Configuration;
var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddCommandLine(args, new Dictionary<string, string>
{
	{ "-ps", SectionName + ":PseudoServer" },
	{ "-rid", SectionName + ":RoomId" },
	{ "-iep", SectionName + ":IPEndPoint" },
});
var app = builder.Build();

var cfg = new Configuration();
app.Configuration.Bind(SectionName, cfg);

app.Logger.LogInformation("房号：{roomid}", cfg.RoomId);
var connBuilder = new BLiveHubConnectionBuilder();
connBuilder.WithRoomid(cfg.RoomId, x =>
{
	x.AcceptedPacketType = FrameVersion.Brotli;
	x.Platform = $".NET {typeof(object).Assembly.GetName().Version}";
});

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
// connection.HandshakeTimeout = TimeSpan.FromSeconds(5);
// connection.KeepAliveInterval = TimeSpan.FromSeconds(35);
Console.CancelKeyPress += async (sender, eargs) =>
{
	Console.WriteLine("已按下退出键，正在断开连接");
	await connection.DisposeAsync();
	Console.WriteLine("已断开连接");
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