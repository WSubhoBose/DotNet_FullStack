namespace BreweryApi.Domain.Exceptions;

public sealed class RequestValidationException : Exception
{
    public RequestValidationException(string message) : base(message)
    {
    }
}