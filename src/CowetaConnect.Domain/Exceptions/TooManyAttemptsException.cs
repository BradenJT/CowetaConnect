// src/CowetaConnect.Domain/Exceptions/TooManyAttemptsException.cs
namespace CowetaConnect.Domain.Exceptions;

public sealed class TooManyAttemptsException : Exception
{
    public int RetryAfterSeconds { get; }

    public TooManyAttemptsException(int retryAfterSeconds = 900)
        : base($"Too many failed attempts. Retry after {retryAfterSeconds} seconds.")
    {
        RetryAfterSeconds = retryAfterSeconds;
    }
}
