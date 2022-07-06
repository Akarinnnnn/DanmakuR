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
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Text.Json;

using static DanmakuR.Protocol.BLiveMessageParser;


namespace DanmakuR.Protocol;

public partial class BLiveProtocol : IHubProtocol, IDisposable
{
	private readonly BLiveOptions options;
	private readonly ILogger logger;

	public string Name => ProtocolName;

	public int Version => SupportedProtocolVersion;
	public TransferFormat TransferFormat => TransferFormat.Binary;
	private MessagePackage message_package = default;
	private bool disposedValue;

	private ReadOnlySequence<byte> decompressed_messages;
	private MemoryBufferWriter.WrittenSequence decompressed;
	private readonly MemoryBufferWriter decompress_writer = new(8192);

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
		if (!message_package.IsCompleted)
		{
			return HandleAggreatedMessages(binder, out message);
		}
		else
		{
			if (decompressed_messages.IsEmpty)
			{
				decompressed.Dispose();
				decompressed = default;
			}
		}

		
	}

	private bool HandleAggreatedMessages(IInvocationBinder binder, out HubMessage? message)
	{
		try
		{
			Log.MultipleMessage(logger);
			var pos = ParseInvocation(message_package.ReadOne(), binder, out message);
			message_package.FitNextRecord(pos);
			return true;
		}
		catch (JsonException)
		{
			Log.InvalidJson(logger);
			message = null;
			return false;
		}
	}

	private bool ParseMessageCore(IInvocationBinder binder, out HubMessage? message, ref ReadOnlySequence<byte> input)
	{
		if (TrySliceInput(in input, out var payload, out var header))
		{
			if (header.Version == FrameVersion.Int32BE || header.OpCode == OpCode.Pong)
			{
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
				if (header.Version != FrameVersion.Json)
				{
					ProcessDecompressedData(ref header, payload);
					input = input.Slice(header.FrameLength);
					if (!TrySliceInput(decompressed_messages, out payload, out header))
					{
						return InvalidMessage(out message);
					}
				}
					// todo
				var pos = ParseInvocation(message_package.ReadOne(), binder, out message);

				if (pos.Equals(payload.End))
					return true;
				else
					message_package.FitNextRecord(pos);
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
	}

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
			Debug.Assert(reader.TryReadBigEndian(out value));
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
		catch (ArgumentException ex)
		{
			return new InvocationBindingFailureMessage(null, WellKnownMethods.OnPopularity.Name, ExceptionDispatchInfo.Capture(ex));
		}
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
	private SequencePosition ParseInvocation(Utf8JsonReader reader, IInvocationBinder binder, out HubMessage msg)
	{
		throw new NotImplementedException();

#pragma warning disable CS0162 // 检测到无法访问的代码
		//TODO: 解析json
		return reader.Position;
#pragma warning restore CS0162 // 检测到无法访问的代码
	}

	// TODO: 什么玩意？
	private void ProcessDecompressedData(ref FrameHeader header, in ReadOnlySequence<byte> compressedPackage)
	{
		switch (header.Version)
		{
			case FrameVersion.Deflate:
				compressedPackage.DecompressDeflate(decompress_writer);
				break;
			case FrameVersion.Brotli:
				compressedPackage.DecompressBrotli(decompress_writer);
				break;
			default:
				throw new InvalidDataException(string.Format(SR.Unreconized_Compression, header._version));
		}

		decompressed = decompress_writer.DeatchToSequence();
		decompressed_messages = decompressed.GetSequence();

		if (!TrySliceInput(in decompressed_messages, out var payload, out header))
			throw new InvalidDataException(SR.Invalid_MsgBag);
		else
			message_package = new(payload.End, header.OpCode);
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

	protected virtual void Dispose(bool disposing)
	{
		if (!disposedValue)
		{
			if (disposing)
			{
				decompressed.Dispose();
				decompress_writer.Dispose();
			}

			disposedValue = true;
		}
	}

	public void Dispose()
	{
		// 不要更改此代码。请将清理代码放入“Dispose(bool disposing)”方法中
		Dispose(disposing: true);
		GC.SuppressFinalize(this);
	}
}
