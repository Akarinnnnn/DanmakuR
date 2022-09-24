using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.SignalR.Protocol;
using System.Buffers;
using System.Buffers.Binary;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost
	.UseKestrel()
	.UseSockets()
	.ConfigureKestrel(o =>
	{
		o.ListenLocalhost(2243);
	});

// builder.Services.Add
var app = builder.Build();
var listenerFac = app.Services.GetRequiredService<IConnectionListenerFactory>();
var listener = await listenerFac.BindAsync(new IPEndPoint(IPAddress.Loopback, 2243));

app.Logger.LogInformation("已启动服务器，监听{}", listener.EndPoint);
var samples = Directory.GetFiles("samples");

_ = Task.Run(async () =>
{
	while (true)
	{
		await Task.Delay(10000);
		app.Logger.LogInformation("服务器正在运行");
	}
});

while (true)
{
	var ctx = await listener.AcceptAsync();
	ArgumentNullException.ThrowIfNull(ctx);
	_ = SvHello(ctx);
}

bool SkipFrame(ref ReadOnlySequence<byte> seq)
{
	var r = new SequenceReader<byte>(seq);
	if (r.TryReadBigEndian(out int frameLength))
	{
		if (frameLength <= seq.Length)
		{
			seq = seq.Slice(frameLength);
			return true;
		}
	}

	return false;
}

async Task SvHello(ConnectionContext ctx)
{
	bool clHelloGot = false;
	while (!clHelloGot)
	{
		app.Logger.LogInformation("客户端({remoteEndPoint})连接", ctx.RemoteEndPoint);
		var input = ctx.Transport.Input;
		var result = await input.ReadAsync();
		var buffer = result.Buffer;
		var examined = buffer.End;
		var consumed = buffer.Start;

		clHelloGot = SkipFrame(ref buffer);
		consumed = buffer.Start;
		input.AdvanceTo(consumed, examined);
	}
	const string response = "{\"code\":0}";
	byte[] messageHeader =
		{
			0, 0, 0, (byte)(16 + response.Length),
			0, 16,
			0, 1,
			0, 0, 0, 8,
			0, 0, 0, 0
		};
	ctx.Transport.Output.Write(messageHeader);
	ctx.Transport.Output.Write(Encoding.UTF8.GetBytes(response));
	await ctx.Transport.Output.FlushAsync();

	_ = SvHandlePing(ctx);
	_ = SvTasks(ctx);
}

async Task SvTasks(ConnectionContext ctx)
{
	while (true)
	{
		await SvKeepAlive(ctx);
		await SvSendCommand(ctx);
	}
}
async Task SvKeepAlive(ConnectionContext ctx)
{
	byte[] pong_message =
		{
			0, 0, 0, 20,
			0, 16,
			0, 1,
			0, 0, 0, 3,
			0, 0, 0, 0,
			0, 0, 0, 250
		};
	app.Logger.LogInformation("发送Ping");
	await Task.Delay(6000);
	ctx.Transport.Output.Write(pong_message);
	await ctx.Transport.Output.FlushAsync();
}


async Task SvSendCommand(ConnectionContext ctx)
{
	byte[] header = new byte[16];
	byte[] buffer = new byte[4096];

	var selected = File.OpenRead(samples[Random.Shared.Next(samples.Length)]);
	BuildHeader(header, unchecked((int)(selected.Length)), 0);
	app.Logger.LogInformation("发送json");

	var output = ctx.Transport.Output;
	output.Write(header);
	while (selected.Position != selected.Length)
	{
		int length = selected.Read(buffer, 0, buffer.Length);
		await output.WriteAsync(buffer[..length]);
	}
	await output.FlushAsync();
	await Task.Delay(Random.Shared.Next(1000, 8000));
}

void BuildHeader(Span<byte> header, int length, byte version)
{
	var target = MemoryMarshal.Cast<byte, int>(header);
	if (BitConverter.IsLittleEndian)
	{
		target[0] = BinaryPrimitives.ReverseEndianness(length + 16);
		header[5] = 16;
		header[7] = version;
		header[11] = 5; // opcode 5 cmd
		header[15] = 0;// seq
	}

}

async Task SvHandlePing(ConnectionContext ctx)
{
	var input = ctx.Transport.Input;
	var result = await input.ReadAsync();
	var buffer = result.Buffer;
	var examined = buffer.End;
	var consumed = buffer.Start;

	SkipFrame(ref buffer);
	consumed = buffer.Start;
	input.AdvanceTo(consumed, examined);
}