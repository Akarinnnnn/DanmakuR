using DanmakuR.Buffer;
using DanmakuR.Protocol.Model;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Protocol;
using Microsoft.Extensions.Options;
using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.IO.Compression;
using System.Numerics;
using System.Text.Json;

namespace DanmakuR.Protocol
{
	internal class BDanmakuProtocol : IHubProtocol
	{
		private readonly BDanmakuOptions options;
		public string Name => typeof(BDanmakuProtocol).FullName!;
		public int Version => 0;
		public TransferFormat TransferFormat => TransferFormat.Binary | TransferFormat.Text;

		public BDanmakuProtocol(IOptions<BDanmakuOptions> opt)
		{
			options = opt.Value;
		}
		/// <inheritdoc/>
		public ReadOnlyMemory<byte> GetMessageBytes(HubMessage message)
		{
			throw new NotImplementedException();
		}

		public bool IsVersionSupported(int version)
		{
			return version == Version;
		}

		public bool TryParseMessage(ref ReadOnlySequence<byte> input, IInvocationBinder binder, [NotNullWhen(true)] out HubMessage? message)
		{
			SequenceReader<byte> r = new(input);
			
			FrameHeader header = new();

			if (r.TryReadBigEndian(out header.FrameLength) && 
				r.TryReadBigEndian(out header.HeaderLength) &&
				r.TryReadBigEndian(out header._version) && 
				r.TryReadBigEndian(out header._opcode) &&
				r.TryReadBigEndian(out header.UnknownField))
			{
				if(header.HeaderLength > 16)
					r.Advance(header.HeaderLength - 16);

				switch (header.Version)
				{
					case FrameVersion.Json:

						break;
					case FrameVersion.Int32BE:
						{
							r.TryReadBigEndian(out int value);
							message = new InvocationMessage("OnPopularity", new object[] { value });
							break;
						}
					case FrameVersion.Deflate:
						{
							using DeflateStream stream = new(new ReadOnlySequenceStream(ref input), CompressionLevel.Optimal);
							
						}
						goto case FrameVersion.Json;
					case FrameVersion.Brotli:
						{
							using BrotliDecoder decoder = new ();
						}
						goto case FrameVersion.Json;
					default:
						message = null;
						return false;
				}
			}
			else
			{
				message = null;
				return false;
			}


			JsonDocument jsondoc = JsonDocument.Parse(input);

			throw new NotImplementedException();
		}

		public void WriteMessage(HubMessage message, IBufferWriter<byte> output)
		{




			throw new NotImplementedException();
		}
	}
}
