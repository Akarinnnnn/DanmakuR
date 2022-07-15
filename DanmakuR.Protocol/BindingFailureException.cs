namespace DanmakuR.Protocol;


[Serializable]
internal class BindingFailureException : Exception
{
	public BindingFailureException() { }
	public BindingFailureException(string message) : base(message) { }
	public BindingFailureException(string message, Exception inner) : base(message, inner) { }
	protected BindingFailureException(
	  System.Runtime.Serialization.SerializationInfo info,
	  System.Runtime.Serialization.StreamingContext context) : base(info, context) { }
}
