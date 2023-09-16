using System.Text.Json;

namespace DanmakuR.Protocol
{
	public static class WellKnownMethods
	{
		public readonly struct MethodDefination
		{
			public readonly string Name;
			public readonly Type[] ParamTypes;

			public MethodDefination(string name, Type[] paramTypes)
			{
				Name = name;
				ParamTypes = paramTypes;
			}
		}

		[Obsolete("人气值已废弃")]
		public static readonly MethodDefination OnPopularity = new(
			nameof(OnPopularity),
			new[] { typeof(int) }
		);

		public static readonly MethodDefination OnMessageJsonDocument = new(
			nameof(OnMessageJsonDocument),
			new[] { typeof(JsonDocument) }
		);

		/// <summary>
		/// 此API用于内部实现，不应直接使用
		/// </summary>
		public static readonly MethodDefination ProtocolOnAggreatedMessage = new(
			"bliveInternal_ProtocolOnAggreatedMessage",
			new[] { typeof(ParsingAggreatedMessageState) }
		);
	}
}
