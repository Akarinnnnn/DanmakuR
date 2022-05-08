using DanmakuR.Buffer;
using DanmakuR.Protocol.Model;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Protocol;
using Microsoft.Extensions.Options;
using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.ExceptionServices;
using System.IO;
using System.IO.Compression;
using System.Numerics;
using System.Text.Json;

using Microsoft.AspNetCore.Internal;
using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using DanmakuR.Resources;
using System.Text;
using Microsoft.AspNetCore.SignalR.Client;

namespace DanmakuR.Protocol
{
	internal class BDanmakuProtocol : IHubProtocol, IHandshakeProtocol
	{
		// int32 framelength, int16 headerlength, int16 version, int32 opcode, int32 seqid
		private static readonly byte[] ping_message =
		{
			0, 0, 0, 16,
			0, 16,
			0, 1,
			0, 0, 0, 2,
			0, 0, 0, 1
		};
		private static ReadOnlyMemory<byte> PingMessageMemory => ping_message;

		private readonly BDanmakuOptions options;
		public string Name => typeof(BDanmakuProtocol).FullName!;
		public int Version => 3;
		public TransferFormat TransferFormat => TransferFormat.Binary;
		

		private MessagePackage message_package;
		
		static BDanmakuProtocol()
		{
			
		}

		public BDanmakuProtocol(IOptions<BDanmakuOptions> opt)
		{
			options = opt.Value;
		}

		/// <inheritdoc/>
		public ReadOnlyMemory<byte> GetMessageBytes(HubMessage message)
		{
			if (ReferenceEquals(message, PingMessage.Instance) || message is PingMessage)
				return PingMessageMemory;

			return HubProtocolExtensions.GetMessageBytes(this, message);
		}

		/// <inheritdoc/>
		public bool IsVersionSupported(int version)
		{
			return version <= Version;
		}

		/// <inheritdoc/>
		public bool TryParseMessage(ref ReadOnlySequence<byte> input, IInvocationBinder binder, [NotNullWhen(true)] out HubMessage? message)
		{
			if (!aggreated_messages.IsEmpty)
				return ParseAggreated(binder, out message);

			if (TryParsePackage(ref input, out var payload, out var header))
			{
				if (header.Version == FrameVersion.Int32BE || header.OpCode == OpCode.Pong)
				{
					int value;
					var span = payload.FirstSpan;
					if (span.Length >= 4)
					{
						value = BinaryPrimitives.ReadInt32BigEndian(payload.FirstSpan);
					}
					else
					{
						var r = new SequenceReader<byte>(payload);
						r.TryReadBigEndian(out value);
					}

					try
					{
						AssertMethodParamTypes(binder, nameof(WellKnownMethods.OnPopularity), WellKnownMethods.OnPopularity.ParamTypes);
						message = new InvocationMessage(nameof(WellKnownMethods.OnPopularity), new object[] { value });
						return true;
					}
					catch (Exception ex)
					{
						message = new InvocationBindingFailureMessage(null, WellKnownMethods.OnPopularity.Name, ExceptionDispatchInfo.Capture(ex));
						return true;
					}
				}

				header = UnPackage(header, ref payload, out aggreated_messages);
				package_opcode = header.OpCode;

				return ParseAggreated(binder, out message);
			}
			else
			{
				message = null;
				return false;
			}
		}
		private bool ParseAggreated(IInvocationBinder binder, out HubMessage? msg)
		{
			//TODO: 解析json
			msg = null;
			return false;
		}

		// TODO: 什么玩意？
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static FrameHeader UnPackage(FrameHeader header, ref ReadOnlySequence<byte> package, out ReadOnlySequence<byte> content)
		{
			var temp = package;
			switch (header.Version)
			{
				case FrameVersion.Deflate:
					temp.DecompressDeflate();
					break;
				case FrameVersion.Brotli:
					temp.DecompressBrotli();
					break;
				case FrameVersion.Json:
					content = package;
					return header;
				default:
					content = default;
					return header;
			}

			if (!TryParsePackage(ref temp, out content, out header))
				throw new InvalidDataException(SR.Invalid_MsgBag);

			return header;
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="source"></param>
		/// <param name="header"></param>
		/// <param name="content">切出的json/压缩包</param>
		/// <returns></returns>
		[MethodImpl(MethodImplOptions.AggressiveOptimization)]
		private static bool TrySlice(ref ReadOnlySequence<byte> source, FrameHeader header, out ReadOnlySequence<byte> content)
		{
			if (source.Length < header.FrameLength)
			{
				content = default;
				return false;
			}

			content = source.Slice(header.HeaderLength, header.FrameLength);
			return true;
		}

		private static bool TryParsePackage(ref ReadOnlySequence<byte> input, out ReadOnlySequence<byte> payload, out FrameHeader header)
		{
			if (!(input.TryReadHeader(out header) && input.Length > header.FrameLength))
			{
				header = default;
				payload = default;
				return false;
			}

			payload = input.Slice(header.HeaderLength, header.FrameLength);
			input = input.Slice(header.FrameLength);
			return true;
		}

		private static void AssertMethodParamTypes(IInvocationBinder binder, string methodName, Type[] types)
		{
			var actualTypes = binder.GetParameterTypes(methodName);
			if (!types.SequenceEqual(actualTypes))
			{
				StringBuilder sb = new(80);
				foreach (var type in types)
					sb.Append(type.Name).Append(", ");
				string message = string.Format(SR.Sig_Mismatch, methodName, sb);
				throw new ArgumentException(message);
			}
		}

		/// <inheritdoc/>
		/// <exception cref="OverflowException" />
		public void WriteMessage(HubMessage message, IBufferWriter<byte> output)
		{
			if (ReferenceEquals(message, PingMessage.Instance) || message is PingMessage)
				ping_message.CopyTo(output.GetSpan(16));

			var tempBuffer = MemoryBufferWriter.Get();
			try
			{
				FrameHeader header = new();
				WriteMessageCore(message, tempBuffer, ref header);
				output.WriteHeader(ref header);
				tempBuffer.CopyTo(output);
			}
			finally
			{
				MemoryBufferWriter.Return(tempBuffer);
			}

			throw new NotImplementedException();
		}

		private void WriteRequestMessageCore(HandshakeRequestMessage m, MemoryBufferWriter temp, ref FrameHeader header)
		{

			if (m.Protocol == Name && m.Version == Version)
			{
				switch (options.HandshakeSettings)
				{
					case Handshake3 v3:
						v3.Serialize(temp);
						break;
					default:
						options.HandshakeSettings.Serialize(temp);
						break;
				}
				header.OpCode = OpCode.ConnectAndAuth;
				header.FrameLength = checked((int)(header.HeaderLength + temp.Length));
			}
			else
			{
				throw new HubException(string.Format(SR.Protocol_Mismatch,
					m.Protocol,
					m.Version.ToString(),
					Name,
					Version.ToString()
					));
			}
		}

		private void WriteMessageCore(HubMessage message, MemoryBufferWriter temp, ref FrameHeader header)
		{
			switch (message)
			{
				case HandshakeRequestMessage m:
					WriteRequestMessageCore(m, temp, ref header);
					return;
				// 这不科学
				case HandshakeResponseMessage:
				case CloseMessage:
					break;

				default:
					throw new HubException(SR.Unsupported_Message);
			}

			header.FrameLength = checked((int)(header.HeaderLength + temp.Length));
		}

		public void WriteRequestMessage(HandshakeRequestMessage requestMessage, IBufferWriter<byte> output)
		{
			MemoryBufferWriter writer = MemoryBufferWriter.Get();
			FrameHeader hreder = new();
			try
			{
				WriteRequestMessageCore(requestMessage, writer, ref hreder);
			}
			finally
			{
				MemoryBufferWriter.Return(writer);
			}
		}

		public bool TryParseResponseMessage(ref ReadOnlySequence<byte> buffer, [NotNullWhen(true)] out HandshakeResponseMessage? responseMessage)
		{
			bool result = TryParsePackage(ref buffer, out var response, out FrameHeader header);
			if (!result)
			{
				responseMessage = null;
				return false;
			}

			responseMessage = new HandshakeResponseMessage(null);
			return true;
		}
	}
}
