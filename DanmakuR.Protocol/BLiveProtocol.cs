using DanmakuR.Protocol.Buffer;
using DanmakuR.Protocol.Buffer.Writers;
using DanmakuR.Protocol.Model;
using DanmakuR.Protocol.Resources;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Protocol;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Buffers;
using System.Buffers.Binary;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Text.Json;

using static DanmakuR.Protocol.BLiveMessageParser;


namespace DanmakuR.Protocol;

public partial class BLiveProtocol : IHubProtocol
{
	private readonly BLiveOptions options;
	private readonly ILogger logger;

	public string Name => ProtocolName;

	public int Version => SupportedProtocolVersion;
	public TransferFormat TransferFormat => TransferFormat.Binary;
	private MessagePackage message_package = default;
	private ReadOnlySequence<byte> decompressed_package = default;

	/// <summary>
	/// 
	/// </summary>
	/// <param name="opt"></param>
	/// <remarks>还是建议使用<see cref="Microsoft.Extensions.DependencyInjection"/>而不是直接<see langword="new"/>一个</remarks>
	public BLiveProtocol(IOptions<BLiveOptions> opt, ILoggerFactory loggerFactory)
	{
		options = opt.Value;
		logger = loggerFactory.CreateLogger<BLiveProtocol>();
	}

	/// <inheritdoc/>
	public ReadOnlyMemory<byte> GetMessageBytes(HubMessage message)
	{
		if (ReferenceEquals(message, PingMessage.Instance) || message is PingMessage)
			return BLiveMessageParser.PingMessageMemory;

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
		if (!message_package.IsCompleted)
		{
			try
			{
				Log.MultipleMessage(logger);
				var pos = ParseOne(message_package.ReadOne(in input), binder, out message);
				message_package.FitNextRecord(ref pos, in input);
				return true;
			}
			catch (JsonException)
			{
				Log.InvalidJson(logger);
				message = null;
				return false;
			}
		}
		else
		{
			input = input.Slice(message_package.End);
		}

		if (BLiveMessageParser.TrySliceInput(in input, out var payload, out var header))
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
					if (!r.TryReadBigEndian(out value))
					{
						message = null;
						return false;
					}
				}

				try
				{
					AssertMethodParamTypes(binder, nameof(WellKnownMethods.OnPopularity), WellKnownMethods.OnPopularity.ParamTypes);
					message = new InvocationMessage(nameof(WellKnownMethods.OnPopularity), new object[] { value });
					return true;
				}
				catch (ArgumentException ex)
				{
					message = new InvocationBindingFailureMessage(null, WellKnownMethods.OnPopularity.Name, ExceptionDispatchInfo.Capture(ex));
					return true;
				}
			}

			try
			{
				if (header.Version != FrameVersion.Json)
				{
					UnPackage(ref header, payload);
				}
			}
			catch (JsonException)
			{
				Log.InvalidJson(logger);
				goto fail;
			}
			catch (InvalidDataException)
			{
				Log.InvalidData(logger);
				goto fail;
			}
		}
	fail:
		message = null;
		return false;
	}


	/// <summary>
	/// 解析核心
	/// </summary>
	/// <param name="reader"></param>
	/// <param name="binder"></param>
	/// <param name="msg"></param>
	/// <returns></returns>
	[SuppressMessage("Performance", "CA1822:将成员标记为 static", Justification = "还没写完")]
	[SuppressMessage("Style", "IDE0060:删除未使用的参数", Justification = "同上")]
	private SequencePosition ParseOne(Utf8JsonReader reader, IInvocationBinder binder, out HubMessage msg)
	{
		throw new NotImplementedException();

#pragma warning disable CS0162 // 检测到无法访问的代码
		//TODO: 解析json
		return reader.Position;
#pragma warning restore CS0162 // 检测到无法访问的代码
	}

	// TODO: 什么玩意？
	private void UnPackage(ref FrameHeader header, ReadOnlySequence<byte> compressedPackage)
	{
		switch (header.Version)
		{
			case FrameVersion.Deflate:
				compressedPackage.DecompressDeflate(out decompressed_package);
				break;
			case FrameVersion.Brotli:
				compressedPackage.DecompressBrotli(out decompressed_package);
				break;
			default:
				throw new InvalidDataException(string.Format(SR.Unreconized_Compression, header._version));
		}
		if (!decompressed_package.TryReadHeader(out header) || !TrySlicePayload(ref decompressed_package, out var opcode))
			throw new InvalidDataException(SR.Invalid_MsgBag);
		else
			message_package = new(decompressed_package.End, opcode);
	}

	private static void AssertMethodParamTypes(IInvocationBinder binder, string methodName, Type[] types)
	{
		var actualTypes = binder.GetParameterTypes(methodName);
		if (!types.SequenceEqual(actualTypes))
		{
			DefaultInterpolatedStringHandler sb = new(
				(actualTypes.Count - 1) * 2, actualTypes.Count, null,
				stackalloc char[actualTypes.Count * 24 + 8]
			);
			foreach (var type in types)
			{
				sb.AppendFormatted(type.Name);
				sb.AppendLiteral(", ");
			}
			string message = string.Format(SR.Sig_Mismatch, methodName, sb.ToStringAndClear());
			throw new ArgumentException(message);
		}
	}

	/// <inheritdoc/>
	/// <exception cref="OverflowException" />
	public void WriteMessage(HubMessage message, IBufferWriter<byte> output)
	{
		if (ReferenceEquals(message, PingMessage.Instance) || message.GetType() == typeof(PingMessage))
			PingMessageSpan.CopyTo(output.GetSpan(16));

		var tempBuffer = MemoryBufferWriter.Get();
		try
		{
			FrameHeader header = new();
			WriteMessageCore(message, tempBuffer, ref header);
			header.WriteToOutput(output);
			tempBuffer.CopyTo(output);
		}
		finally
		{
			MemoryBufferWriter.Return(tempBuffer);
		}
	}

	[SuppressMessage("Performance", "CA1822:将成员标记为 static", Justification = "也没写完")]
	private void WriteMessageCore(HubMessage message, MemoryBufferWriter temp, ref FrameHeader header)
	{
		switch (message)
		{
			// 这不科学
			case HandshakeResponseMessage:
			case CloseMessage:
				break;

			default:
				throw new HubException(SR.Unsupported_Message);
		}

		header.FrameLength = checked((int)(header.HeaderLength + temp.Length));
	}
}
