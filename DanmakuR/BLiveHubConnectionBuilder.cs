﻿using DanmakuR.Connection;
using DanmakuR.Protocol;
using DanmakuR.Protocol.Model;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.AspNetCore.SignalR.Protocol;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace DanmakuR
{
	public class BLiveHubConnectionBuilder : IHubConnectionBuilder
	{
		public BLiveHubConnectionBuilder()
		{
			Services.AddLogging()
				.AddSingleton<HubConnection>();
			this.PrepareForBLiveProtocol();
		}

		private bool isBuilt = false;

		private ServiceCollection ServicesInternal { get; } = new ServiceCollection();

		public IServiceCollection Services => ServicesInternal;
		public BLiveProtocol? HubProtocol { get; private set; } = null;

		[MemberNotNull(nameof(HubProtocol))]
		public HubConnection Build()
		{
			if (isBuilt)
				throw new InvalidOperationException(
					$"同一个{nameof(BLiveHubConnectionBuilder)}" +
					$"只能创建一个{nameof(HubConnection)}。" +
					$"这是{nameof(HubConnection)}的限制。"
				);

			Services.Wrap<IConnectionFactory>().AddSingleton<IConnectionFactory, RewriteConnectionContextFactory>();
			ServicesInternal.MakeReadOnly();

			var provider = Services.BuildServiceProvider();

			_ = provider.GetService<IConnectionFactory>() ??
				throw new InvalidOperationException($"无法创建{nameof(HubConnection)}实例，" +
				$"缺少{nameof(IConnectionFactory)}服务。");

			bool hasNoHandshake2 = provider.GetService<IOptions<Handshake2>>() == null;

			if (!hasNoHandshake2 /* && TODO openPlatformNotConfigured */)
			{
				throw new InvalidOperationException($"无法创建{nameof(HubConnection)}实例，" +
					$"未配置{nameof(IOptions<Handshake2>)}。" +
					$"是否忘记调用{nameof(DanmakuRExtensions)}.{nameof(DanmakuRExtensions.WithRoomid)}？");
			}

			HubProtocol = provider.GetService<IHubProtocol>() as BLiveProtocol ?? 
				throw new InvalidOperationException($"无法创建{nameof(HubConnection)}实例，" +
				$"必须使用{nameof(BLiveProtocol)}作为{nameof(IHubProtocol)}的实现。");

			var connection = provider.GetRequiredService<HubConnection>();

			connection.On(WellKnownMethods.ProtocolOnAggregatedMessage.Name, 
				new Func<ParsingAggregatedMessageState, Task>(BLiveProtocol.HandleAggreatedMessages));

			isBuilt = true;

			return connection;
		}
	}
}
