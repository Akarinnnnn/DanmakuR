using System.Text.Json;

namespace DanmakuR.Protocol
{
	public static class WellKnownMethods
	{
		public readonly struct MethodDefination
		{
			public readonly string Name;
			[Obsolete("此成员可变，存在被修改的风险")]
			public readonly Type[] ParamTypes;
			public readonly IReadOnlyList<Type> ReadonlyParamTypes;

			public MethodDefination(string name, Type[] paramTypes)
			{
				Name = name;
#pragma warning disable CS0618 // 类型或成员已过时
				ParamTypes = paramTypes;
				ReadonlyParamTypes = [.. ParamTypes];
#pragma warning restore CS0618 // 类型或成员已过时
			}
		}

		[Obsolete("人气值已废弃")]
		public static readonly MethodDefination OnPopularity = new(
			nameof(OnPopularity),
			[typeof(int)]
		);

		public static readonly MethodDefination OnMessageJsonDocument = new(
			nameof(OnMessageJsonDocument),
			[typeof(JsonDocument)]
		);

		/// <summary>
		/// 此API用于内部实现，不应直接使用
		/// </summary>
		public static readonly MethodDefination ProtocolOnAggregatedMessage = new(
			"bliveInternal_ProtocolOnAggregatedMessage",
			[typeof(ParsingAggregatedMessageState)]
		);
	}
}
