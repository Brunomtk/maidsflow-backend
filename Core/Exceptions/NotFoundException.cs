namespace Core.Exceptions
{
    /// <summary>
    /// Thrown when a resource is not found.
    /// Maps to HTTP 404.
    /// </summary>
    public class NotFoundException : System.Exception
    {
        public NotFoundException(string message = "Not Found") : base(message) { }
    }
}
