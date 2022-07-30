using Microsoft.AspNetCore.Connections;
using System.Buffers;
using System.Net;
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

	_ = SvKeepAlive(ctx);
	_ = SvHandlePing(ctx);
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
	while (true)
	{
		await Task.Delay(6000);
		ctx.Transport.Output.Write(pong_message);
		await ctx.Transport.Output.FlushAsync();
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