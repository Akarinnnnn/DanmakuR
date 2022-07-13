using Microsoft.AspNetCore.SignalR.Client;

namespace DanmakuR
{
	public class BLiveHubConnectionBuilder : HubConnectionBuilder
	{
		public BLiveHubConnectionBuilder()
		{
			this.UseBLiveProtocol();
			
		}

		public new HubConnection Build()
		{
			var connection = base.Build();

			// Todo: 以后考虑注册一个函数，用来解决一个数据包含有多个信息的问题
			// 现在的做法不符合HubProtocol不能有状态的准则
			// 多线程也容易出现资源争用问题
			return connection;
		}
	}
}
