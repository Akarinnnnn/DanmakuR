using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace DanmakuR.Connection;
#pragma warning disable IDE1006 // b站命的名

public class NegotiateResponse
{
	[JsonConstructor]
	public NegotiateResponse(int code, string message, NegotiateData data)
	{
		this.code = code;
		this.message = message;
		this.data = data;
	}

	public int code { get; set; }
	public string? message { get; set; }
	public NegotiateData? data { get; set; }

	[JsonIgnore]
	[MemberNotNullWhen(true, nameof(data))]
	public bool IsValid => code == 0;
}

public class NegotiateData
{
	[JsonConstructor]
	public NegotiateData(int refresh_rate, int max_delay, string token, Host[] host_list)
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

[JsonSerializable(typeof(NegotiateResponse))]
[JsonSerializable(typeof(NegotiateData))]
[JsonSerializable(typeof(Host))]
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.Unspecified,
	GenerationMode = JsonSourceGenerationMode.Serialization)]
public partial class NegotiateContext : JsonSerializerContext
{
	
}

