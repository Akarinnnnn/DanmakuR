using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DanmakuR.HandshakeProxy
{
	public class HandshakeProxyConnectionOptions
	{
		public TransformData? TransformRequest { get; set; }
		public TransformData? TransformResponse { get; set; }
	}
}
