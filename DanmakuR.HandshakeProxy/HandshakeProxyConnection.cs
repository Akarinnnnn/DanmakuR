using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Options;
using System.IO.Pipelines;

namespace DanmakuR.HandshakeProxy
{
	public sealed class HandshakeProxyConnection : ConnectionContext
	{
		private readonly ConnectionContext backing;

		public HandshakeProxyConnection(ConnectionContext backing, HandshakeProxyConnectionOptions options)
		{
			this.backing = backing;
			ArgumentNullException.ThrowIfNull(options.TransformRequest);
			ArgumentNullException.ThrowIfNull(options.TransformResponse);
			var proxy = new ProxyDuplexPipe(backing.Transport, options.TransformResponse, options.TransformRequest);
			Transport = proxy;
			Task.Run(async () =>
			{
				await Task.WhenAll(proxy.ReadSourceTask, proxy.WriteSourceTask);
				Transport = backing.Transport;
				await proxy.FlushAllAsync();
			});
		}

		public override IDuplexPipe Transport { get; set; }

		public override string ConnectionId { get => backing.ConnectionId; set => backing.ConnectionId = value; }

		public override IFeatureCollection Features => backing.Features;

		public override IDictionary<object, object?> Items { get => backing.Items; set => backing.Items = value; }

		public override ValueTask DisposeAsync()
		{
			return backing.DisposeAsync();
		}
	}
}