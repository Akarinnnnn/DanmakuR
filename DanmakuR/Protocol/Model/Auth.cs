using System.Text.Json.Serialization;
namespace DanmakuR.Protocol.Model
{
	public class Auth
	{
		public long? Aid;
		public long From = 7;
		public int Type = 3;

		[JsonExtensionData]
		public IDictionary<string, object> AdditionalAuthParams { get; set; }

		public Auth(IDictionary<string, object> authParams)
		{
			AdditionalAuthParams = authParams;
		}

		public void EnsureValid()
		{
			Type = 3;
		}
	}
}
