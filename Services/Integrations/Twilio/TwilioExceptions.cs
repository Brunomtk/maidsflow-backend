namespace Services.Integrations.Twilio;

/// <summary>
/// Exceptions used by the Twilio SMS integration so controllers can map errors to proper HTTP codes
/// without leaking credentials or verbose upstream payloads.
/// </summary>
public class TwilioConfigurationException : Exception
{
    public TwilioConfigurationException(string message) : base(message) { }
}

public class TwilioValidationException : ArgumentException
{
    public TwilioValidationException(string message, string? paramName = null)
        : base(message, paramName)
    {
    }
}

public class TwilioRequestException : Exception
{
    public int StatusCode { get; }
    public string? ResponseBody { get; }

    public TwilioRequestException(int statusCode, string message, string? responseBody = null)
        : base(message)
    {
        StatusCode = statusCode;
        ResponseBody = responseBody;
    }
}
