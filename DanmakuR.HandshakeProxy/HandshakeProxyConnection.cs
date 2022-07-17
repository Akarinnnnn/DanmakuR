using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Options;
using System.IO.Pipelines;

namespace DanmakuR.HandshakeProxy
{
	public sealed class HandshakeProxyConnection : ConnectionContext
	{
		private readonly ConnectionContext backing;
		private readonly RewriteHandshakeDuplexPipe rewrite_handler;

		public HandshakeProxyConnection(ConnectionContext backing, HandshakeProxyConnectionOptions options)
		{
			this.backing = backing;
			ArgumentNullException.ThrowIfNull(options.TransformRequest);
			ArgumentNullException.ThrowIfNull(options.TransformResponse);
			rewrite_handler = new RewriteHandshakeDuplexPipe(backing.Transport, options.TransformResponse, options.TransformRequest);
			Transport = rewrite_handler;
			RewriteAndRedirectTask = Start();
		}

		private async Task Start()
		{
			await rewrite_handler.RewriteTask;
			Transport = backing.Transport;
			await rewrite_handler.FlushAllAsync();
		}


		public override IDuplexPipe Transport { get; set; }

		public override string ConnectionId { get => backing.ConnectionId; set => backing.ConnectionId = value; }

		public override IFeatureCollection Features => backing.Features;

		public override IDictionary<object, object?> Items { get => backing.Items; set => backing.Items = value; }
		public Task RewriteAndRedirectTask { get; }
		public override async ValueTask DisposeAsync()
		{
			try
			{
				await RewriteAndRedirectTask;
			}
			catch
			{

			}
			finally
			{
				await backing.DisposeAsync();
			}
		}
	}
}