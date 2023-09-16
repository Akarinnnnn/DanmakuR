# DanmakuR
通过`HubConnection`连接到b站直播间弹幕信息流，传输方式可以是`Websocket`或`TCP`之一。

## 快速上手
首先，通过nuget查找并安装安装`DanmakuR`包。  

```csharp
// 1. 创建`HubConnenction`    

using DanmakuR;

var connBuilder = new BLiveHubConnectionBuilder();

connBuilder.UseWebsocketTransport()
	.WithRoomid(2233);// 基本用法

HubConnection connection = connBuilder.Build();

// 2. 响应事件
// Listener.cs
class Listener : IDanmakuSource
{
	public async Task OnMessageJsonDocumentAsync(string messageName, JsonDocument message)
	{
		switch (messageName)
		{
			// ...
		}
	}
}

// 3. 绑定监听器并启动连接
listener.BindToConnection(connection);
await connection.StartAsync();
```

## TCP连接？
目前，建立TCP连接需要ASP.NET Core的Kestrel（Microsoft.AspNetCore.Server.Kestrel.Transport.Sockets.SocketConnectionFactory类），也就是说只能在ASP.NET Core服务器上用。

#### 那维护呢
随缘