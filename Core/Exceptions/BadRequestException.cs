namespace Core.Exceptions
{
    /// <summary>
    /// Thrown when the request is invalid.
    /// Maps to HTTP 400.
    /// </summary>
    public class BadRequestException : System.Exception
    {
        public BadRequestException(string message = "Bad Request") : base(message) { }
    }
}
