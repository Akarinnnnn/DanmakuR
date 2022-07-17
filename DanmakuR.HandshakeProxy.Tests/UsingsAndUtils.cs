global using Xunit;
global using Moq;
global using System.IO.Pipelines;
global using System.Buffers;
global using System;
global using static DanmakuR.HandshakeProxy.Tests.TestHelpers;

using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Http.Features;

namespace DanmakuR.HandshakeProxy.Tests;
public class PipeHolder : IDuplexPipe
{
	public PipeReader Input { get; set; } = null!;
	public PipeWriter Output { get; set; } = null!;
}

public class TestConnectionContext : ConnectionContext
{
	public override IDuplexPipe Transport { get; set; }

	public TestConnectionContext(IDuplexPipe transport)
	{
		Transport = transport;
	}

	public override string ConnectionId { get; set; } = "114 514 1919 810";
	public override IFeatureCollection Features { get; } = null!;
	public override IDictionary<object, object?> Items { get; set; } = null!;
}