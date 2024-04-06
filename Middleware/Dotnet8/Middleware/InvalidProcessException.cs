using System.Runtime.Serialization;

namespace Middleware;

public class InvalidProcessException : Exception
{
    public string ProcessName { get; }
    public InvalidProcessException() : base("Invalid process") { }
    public InvalidProcessException(string message) : base(message) { }
    public InvalidProcessException(string message, string processName) : base(message)
    {
        ProcessName = processName;
    }
    public InvalidProcessException(string message, Exception innerException) : base(message, innerException)
    {
    }
}