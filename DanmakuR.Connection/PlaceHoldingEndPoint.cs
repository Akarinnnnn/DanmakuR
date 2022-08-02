using System.Net;
using System.Net.Sockets;

namespace DanmakuR.Connection
{
	public class PlaceHoldingEndPoint : EndPoint
	{
		public override AddressFamily AddressFamily => AddressFamily.Unspecified;
	}

}