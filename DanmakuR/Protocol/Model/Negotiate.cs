
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace DanmakuR.Protocol.Model;
#pragma warning disable IDE1006 // b站命的名

internal class Negotiate
{
	[JsonConstructor]
	public Negotiate(int code, string message, NegotiateData data)
	{
		this.code = code;
		this.message = message;
		this.data = data;
	}

	internal int code { get; set; }
	internal string message { get; set; }
	internal NegotiateData? data { get; set; }

	[JsonIgnore]
	[MemberNotNullWhen(true, nameof(data))]
	internal bool IsValid => code == 0;
}

internal class NegotiateData
{
	[JsonConstructor]
	public NegotiateData(int refresh_rate, int max_delay, string token, Host[] host_list)
	{
		this.refresh_rate = refresh_rate;
		this.max_delay = max_delay;
		this.token = token;
		this.host_list = host_list;
	}
	internal int refresh_rate { get; set; }
	/// <summary>连接后，发送Handshake的最长等待时长</summary>
	internal int max_delay { get; set; }
	internal string token { get; set; }
	internal Host[] host_list { get; set; }
}

internal class Host
{
	[JsonConstructor]
	public Host(string host, short port, short wss_port, short ws_port)
	{
		this.host = host;
		this.port = port;
		this.wss_port = wss_port;
		this.ws_port = ws_port;
	}

	internal string host { get; set; }
	internal short port { get; set; }
	internal short wss_port { get; set; }
	internal short ws_port { get; set; }
}
