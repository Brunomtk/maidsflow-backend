namespace Core.Exceptions
{
    /// <summary>
    /// Thrown when an upstream integration returns an error (e.g., Guesty).
    /// Maps to HTTP 502.
    /// </summary>
    public class BadGatewayException : System.Exception
    {
        public BadGatewayException(string message = "Bad Gateway") : base(message) { }
    }
}
