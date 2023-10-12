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
				$"rid-{roomid}-{DateTime.Now:yyyy-MM-dd HH-mm-ss}"
			);
			basepath = Path.GetFullPath(basepath);
			Directory.CreateDirectory(basepath);
		}

		public async Task OnMessageJsonDocumentAsync(string cmdName, JsonDocument message)
		{
			using var _ = message;
			ulong currentId = Interlocked.Increment(ref msgid);
			int msgHashcode = message.GetHashCode();
			logger.LogInformation("{currentId}-{cmdName}-{msgHashcode}", cmdName, currentId, msgHashcode);

			using var stream = File.OpenWrite(Path.Combine(basepath, $"{currentId}-{cmdName}.json"));
			await JsonSerializer.SerializeAsync(stream, message, serializerOptions);
			logger.LogInformation("{currentId}-{cmdName}-{msgHashcode} 保存完成", cmdName, currentId, msgHashcode);
		}

		public Task OnPopularityAsync(int popularity)
		{
			logger.LogInformation("气人值：{popularity}", popularity);
			return Task.CompletedTask;
		}
	}
}
