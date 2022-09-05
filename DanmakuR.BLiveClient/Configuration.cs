using Microsoft.AspNetCore.Connections;
using System.Net;

namespace DanmakuR.BLiveClient
{
	public class Configuration
	{
		public const string SectionName = "BLiveClient";

		public int RoomId { get; set; } = 1;
		public IPEndPoint? IPEndPoint { get; set; }
		public UriEndPoint? UriEndPoint { get; set; }
		public bool ShortId { get; set; }

		public bool PseudoServer { get; set; } = false;
	}
}
