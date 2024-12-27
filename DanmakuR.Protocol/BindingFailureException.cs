namespace DanmakuR.Protocol;


[Serializable]
internal class BindingFailureException : Exception
{
	public BindingFailureException() { }
	public BindingFailureException(string message) : base(message) { }
	public BindingFailureException(string message, Exception inner) : base(message, inner) { }
}
