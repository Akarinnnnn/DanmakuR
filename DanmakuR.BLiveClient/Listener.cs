using System.Text.Json;
using System.Text.Encodings.Web;
namespace DanmakuR.BLiveClient
{
	public class Listener : IDanmakuSource
	{
		private static readonly JsonSerializerOptions serializerOptions = new(JsonSerializerDefaults.General)
		{
			Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
			WriteIndented = true,
			ReadCommentHandling = JsonCommentHandling.Skip
		};
		private readonly ILogger<Listener> logger;
		private readonly string basepath;
		private ulong msgid = 0;

		public ulong CurrentMessageId => msgid;

		public Listener(ILogger<Listener> logger, int roomid)
		{
			this.logger = logger;
			basepath = Path.Combine(
				Path.GetDirectoryName(Environment.ProcessPath) 
				?? Environment.CurrentDirectory, 
				$"rid-{roomid}"
			);
			basepath = Path.GetFullPath(basepath);
			Directory.CreateDirectory(basepath);
		}

		public async Task OnMessageJsonDocumentAsync(string messageName, JsonDocument message)
		{
			ulong currentId = Interlocked.Increment(ref msgid);
			using var stream = File.OpenWrite(Path.Combine(basepath, $"{currentId}-{messageName}"));
			await JsonSerializer.SerializeAsync(stream, message, serializerOptions);
		}

		public Task OnPopularityAsync(int popularity)
		{
			logger.LogInformation("气人值：{popularity}", popularity);
			return Task.CompletedTask;
		}
	}
}
