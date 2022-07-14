﻿namespace DanmakuR.Protocol
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

		public static readonly MethodDefination OnPopularity = new(
			nameof(OnPopularity),
			new[] { typeof(int) }
		);

		/// <summary>
		/// 此API用于内部实现，不应直接使用
		/// </summary>
		public static readonly MethodDefination ProtocolOnAggreatedMessage = new("bliveInternal_ProtocolOnAggreatedMessage",
			new[] { typeof(ParsingAggreatedMessageState) }
		);
	}
}
