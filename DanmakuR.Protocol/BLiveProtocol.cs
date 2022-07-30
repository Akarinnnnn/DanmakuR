using DanmakuR.Protocol.Buffer;
using DanmakuR.Protocol.Model;
using DanmakuR.Protocol.Resources;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Protocol;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Buffers;
using System.Buffers.Binary;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Text.Json;
using System.Threading.Channels;
using static DanmakuR.Protocol.BLiveMessageParser;
using MemoryBufferWriter = DanmakuR.Protocol.Buffer.Writers.MemoryBufferWriter;

namespace DanmakuR.Protocol;

public partial class BLiveProtocol : IHubProtocol
{
	private readonly BLiveOptions options;
	private readonly ILogger logger;

	public string Name => ProtocolName;

	public int Version => SupportedProtocolVersion;
	public TransferFormat TransferFormat => TransferFormat.Binary;

	private static readonly UnboundedChannelOptions channel_options = new()
	{
		SingleReader = true,
		SingleWriter = false,
		AllowSynchronousContinuations = true
	};
	private readonly Channel<HubMessage> hubmessage_channel = Channel.CreateUnbounded<HubMessage>(channel_options);

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
		if (hubmessage_channel.Reader.TryRead(out message))
			return true;

		return ParseMessageCore(binder, out message, ref input);
	}

	/// <devdoc>
	/// <param name="binder"></param>
	/// <param name="message"></param>
	/// <param name="input">可能是压缩的数据包</param>
	/// <returns></returns>
	/// </devdoc>
	private bool ParseMessageCore(IInvocationBinder binder, out HubMessage? message, ref ReadOnlySequence<byte> input)
	{
		if (TrySliceInput(in input, out var payload, out var header))
		{
			if (header.Version == FrameVersion.Int32BE || header.OpCode == OpCode.Pong)
			{
				// 收到int32气人值
				if (payload.Length < 4)
				{
					return InsufficientDataToParse(out message);
				}

				int value = ParsePongValue(in payload);
				message = MakePongMessage(binder, value);
				input = input.Slice(header.FrameLength);
				return true;
			}

			try
			{
				// 收到Message数据包，转化为InvocationMessage
				Debug.Assert(header.OpCode == OpCode.Message);
				if (header.Version != FrameVersion.Json)
				{
					bool isBr = header.Version == FrameVersion.Brotli;
					var holder = DecompressData(in header, payload);
					input = input.Slice(header.FrameLength);

					return BindAggreatedMessage(binder, out message, new(
						this,
						hubmessage_channel,
						holder,
						isBr,
						binder
					));
				}

				return HandleInvocation(binder, out message, payload);
			}
			catch (JsonException)
			{
				Log.InvalidJson(logger);
				return InvalidMessage(out message);
			}
			catch (InvalidDataException)
			{
				Log.InvalidData(logger);
				return InvalidMessage(out message);
			}
		}
		else
		{
			return InsufficientDataToParse(out message);
		}
	}

	private static bool BindAggreatedMessage(IInvocationBinder binder, out HubMessage message, ParsingAggreatedMessageState state)
	{
		try
		{
			AssertMethodParamTypes(binder, WellKnownMethods.ProtocolOnAggreatedMessage.Name, WellKnownMethods.ProtocolOnAggreatedMessage.ParamTypes);
			message = new InvocationMessage(WellKnownMethods.ProtocolOnAggreatedMessage.Name, new object[] { state });
		}
		catch (BindingFailureException ex)
		{
			message = new InvocationBindingFailureMessage(null, WellKnownMethods.ProtocolOnAggreatedMessage.Name, ExceptionDispatchInfo.Capture(ex));
		}

		return true;
	}

	internal bool HandleInvocation(IInvocationBinder binder, out HubMessage? message, in ReadOnlySequence<byte> payload)
	{
		try
		{
			var pos = ParseInvocation(new(payload), binder, out message);
			return true;
		}
		catch (InvalidDataException)
		{
			Log.NotAnInvocation(logger);
			return InvalidMessage(out message);
		}

	}

	[MethodImpl(MethodImplOptions.AggressiveOptimization)]
	private static int ParsePongValue(in ReadOnlySequence<byte> payload)
	{
		ReadOnlySpan<byte> span = payload.FirstSpan;
		int value;
		if (span.Length >= 4)
		{
			value = BinaryPrimitives.ReadInt32BigEndian(span);
		}
		else
		{
			var reader = new SequenceReader<byte>(payload);
			reader.TryReadBigEndian(out value);
		}

		return value;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static HubMessage MakePongMessage(IInvocationBinder binder, int value)
	{
		try
		{
			AssertMethodParamTypes(binder, nameof(WellKnownMethods.OnPopularity), WellKnownMethods.OnPopularity.ParamTypes);
			return new InvocationMessage(nameof(WellKnownMethods.OnPopularity), new object[] { value });
		}
		catch (BindingFailureException ex)
		{
			return new InvocationBindingFailureMessage(null, WellKnownMethods.OnPopularity.Name, ExceptionDispatchInfo.Capture(ex));
		}
	}

	private static MemoryBufferWriter.WrittenSequence DecompressData(in FrameHeader header, in ReadOnlySequence<byte> compressedPackage)
	{
		var writer = MemoryBufferWriter.Get();
		try
		{
			switch (header.Version)
			{
				case FrameVersion.Deflate:
					compressedPackage.DecompressDeflate(writer);
					break;
				case FrameVersion.Brotli:
					compressedPackage.DecompressBrotli(writer);
					break;
				default:
					throw new InvalidDataException(string.Format(SR.Unreconized_Compression, header._version));
			}

			return writer.DeatchToSequence();
		}
		finally
		{
			MemoryBufferWriter.Return(writer);
		}
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
			throw new BindingFailureException(message);
		}
	}

	/// <inheritdoc/>
	/// <exception cref="OverflowException" />
	public void WriteMessage(HubMessage message, IBufferWriter<byte> output)
	{
		if (ReferenceEquals(message, PingMessage.Instance) || message.GetType() == typeof(PingMessage))
		{
			output.Write(PingMessageSpan);
			return;
		}

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
			case CloseMessage:
				break;
			case PingMessage:
				Debug.Fail("PingMessage不应该漏到这");
				break;
			default:
				Debugger.Break();
				throw new HubException(SR.Unsupported_Message);
		}

		header.FrameLength = checked((int)(header.HeaderLength + temp.Length));
	}
}
