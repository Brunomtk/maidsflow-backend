namespace Core.Exceptions
{
    /// <summary>
    /// Thrown when an authenticated user is not allowed to access a resource.
    /// Maps to HTTP 403.
    /// </summary>
    public class ForbiddenException : System.Exception
    {
        public ForbiddenException(string message = "Forbidden") : base(message) { }
    }
}
