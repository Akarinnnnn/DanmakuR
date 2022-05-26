using System.Text.Json.Serialization;
namespace DanmakuR.Protocol.Model
{
	/// <summary>
	/// 可携带身份信息
	/// </summary>
	public class Handshake3 : Handshake2
	{
		public long? Aid;
		public long? From;

		[JsonExtensionData]
		public IDictionary<string, object> AdditionalAuthParams { get; set; }

		[JsonConstructor]
		public Handshake3()
		{
			AdditionalAuthParams = new Dictionary<string, object>();
		}

		public Handshake3(IDictionary<string, object> authParams)
		{
			AdditionalAuthParams = authParams;
		}

		public new void EnsureValid()
		{
			Type = 3;
			From = From > 0 ? From : 7;
		}
	}
}
