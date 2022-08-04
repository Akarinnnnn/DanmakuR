using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace DanmakuR.Connection;
#pragma warning disable IDE1006 // b站命的名

public class DanmuInfoData
{
	[JsonConstructor]
	public DanmuInfoData(int refresh_rate, int max_delay, string token, Host[] host_list)
	{
		this.refresh_rate = refresh_rate;
		this.max_delay = max_delay;
		this.token = token;
		this.host_list = host_list;
	}
	public int refresh_rate { get; set; }
	/// <summary>连接后，发送Handshake的最长等待时长</summary>
	public int max_delay { get; set; }
	public string token { get; set; }
	public Host[] host_list { get; set; }
}

public struct Host
{
	[JsonConstructor]
	public Host(string host, int port, int wss_port, int ws_port)
	{
		this.host = host;
		this.port = port;
		this.wss_port = wss_port;
		this.ws_port = ws_port;
	}

	public string host { get; set; }
	public int port { get; set; }
	public int wss_port { get; set; }
	public int ws_port { get; set; }

	public static readonly Host[] DefaultHosts = { new("broadcastlv.chat.bilibili.com", 2243, 443, 2244) };
}


public class RoomInitData
{
	public int room_id { get; set; }
	//public int short_id { get; set; }
	//public int uid { get; set; }
	//public int need_p2p { get; set; }
	//public bool is_hidden { get; set; }
	//public bool is_locked { get; set; }
	//public bool is_portrait { get; set; }
	//public int live_status { get; set; }
	//public int hidden_till { get; set; }
	//public int lock_till { get; set; }
	//public bool encrypted { get; set; }
	//public bool pwd_verified { get; set; }
	//public int live_time { get; set; }
	//public int room_shield { get; set; }
	//public int is_sp { get; set; }
	//public int special_type { get; set; }
}


public class ControllerResponse<T> where T : class
{
	public int code { get; set; }
	public string? message { get; set; }
	public T? data { get; set; }

	[JsonIgnore]
	[MemberNotNullWhen(true, nameof(data))]
	[MemberNotNullWhen(false, nameof(message))]
	public bool IsValid => code == 0;

}

[JsonSerializable(typeof(ControllerResponse<DanmuInfoData>))]
[JsonSerializable(typeof(ControllerResponse<RoomInitData>))]
[JsonSerializable(typeof(DanmuInfoData))]
[JsonSerializable(typeof(RoomInitData))]
[JsonSerializable(typeof(Host))]
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.Unspecified,
	GenerationMode = JsonSourceGenerationMode.Metadata)]
public partial class NegotiateContext : JsonSerializerContext
{

}

